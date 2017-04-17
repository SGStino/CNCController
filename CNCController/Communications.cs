using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CNCController.Protocol;
using System.Threading;

namespace CNCController
{
    public class Communications
    {
        private const byte HEADER_SIZE = 4;
        private byte[] header = new byte[] { (byte)'M', (byte)'S', (byte)'G' };

        private CancellationTokenSource cancelCommands = new CancellationTokenSource();
        private SerialPort serial;
        private uint id = 0;
        private readonly Dictionary<ulong, TaskCompletionSource<ulong>> taskAcknowledgements = new Dictionary<ulong, TaskCompletionSource<ulong>>();
        private readonly Dictionary<ulong, TaskCompletionSource<ulong>> taskCompletions = new Dictionary<ulong, TaskCompletionSource<ulong>>();

        private byte sequence = 0;

        private TaskCompletionSource<bool>[] responseTasks = new TaskCompletionSource<bool>[255];

        public void Open(string port, int baudrate = 9600)
        {
            serial = new SerialPort(port, baudrate);
            serial.Open();
            beginReading();
        }

        private async void beginReading()
        {
            byte payloadLength = 0, payloadStart = 0, seq = 0;
            bool hasHeader = false;

            byte offset = 0;
            byte[] buffer = new byte[128];
            do
            {
                int available = buffer.Length - offset;
                int count = await serial.BaseStream.ReadAsync(buffer, offset, available).ConfigureAwait(false);
                offset += (byte)count;
                if (offset >= buffer.Length) throw new OverflowException("Too much data for buffer, should not have happened");
                if (count > 0)
                {
                    if (findHeader(buffer, offset, out byte headerOffset))
                    {
                        payloadLength = buffer[headerOffset + header.Length];
                        seq = buffer[headerOffset + header.Length + 1];
                        payloadStart = (byte)(headerOffset + header.Length + 2);
                        hasHeader = true;
                    }

                    if (hasHeader && offset >= payloadStart + payloadLength)
                    {
                        onReceived(buffer, payloadStart, payloadLength, seq);
                        offset = 0;
                        hasHeader = false;
                    }
                }
            }
            while (serial.IsOpen);
        }

        private void onReceived(byte[] buffer, byte payloadStart, byte length, byte seq)
        {

            if (checkAck(buffer, payloadStart, length, out bool ack, out byte ackSeq))
                confirm(ackSeq, ack);
            else
            {
                var asString = Encoding.ASCII.GetString(buffer, payloadStart, length);

            }
        }

        private void confirm(byte ackSeq, bool ack)
        {
            var res = responseTasks[ackSeq];
            res?.TrySetResult(ack);
            responseTasks[ackSeq] = null;
        }

        private bool checkAck(byte[] buffer, byte payloadStart, byte length, out bool ack, out byte ackSeq)
        {
            if (length == 4)
            {
                var str = Encoding.ASCII.GetString(buffer, payloadStart, 3);
                ackSeq = buffer[payloadStart + 3];
                switch (str)
                {
                    case "ACK":
                        ack = true;
                        return true;
                    case "NAC":
                        ack = false;
                        return true;
                }
            }
            ack = false;
            ackSeq = 0;
            return false;
        }

        private bool findHeader(byte[] buffer, byte offset, out byte headerOffset)
        {
            for (byte i = 0; i < offset - HEADER_SIZE; i++)
            {
                if (checkHeader(buffer, i))
                {
                    headerOffset = i;
                    return true;
                }
            }
            headerOffset = 0;
            return false;
        }

        private bool checkHeader(byte[] buffer, byte i)
        {
            for (byte j = 0; j < header.Length; j++)
            {
                if (buffer[i + j] != header[j])
                    return false;
            }
            return true;
        }

        public event Action<byte[], int, int> RawDataReceived;
        public event Action<byte[], int, int> RawDataSent;

        private SemaphoreSlim writeSemaphore = new SemaphoreSlim(1);

        public async Task WriteAsync<T>(byte seq, T datagram, CancellationToken cancellationToken)
            where T : struct
        {
            await writeSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var bytes = getBytes(seq, datagram, out ushort crc);
                bool confirmed = false;

                var confirm = WaitForResponse(seq);
                do
                {
                    RawDataSent?.Invoke(bytes, 0, bytes.Length);
                    await serial.BaseStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                    var timeout = getConfirmTimeout(); // give remote 100 ms time to confirm. 
                    confirmed = await WaitForAnyAsync(timeout, confirm).ConfigureAwait(false);
                    if (confirm.IsCompleted && !confirmed) // NACK was received, wait for next ACK
                        confirm = WaitForResponse(seq);
                }
                while (!confirmed);
                // TODO: wait for confirmation
            }
            finally
            {
                writeSemaphore.Release();
            }
        }

        private async Task<bool> WaitForAnyAsync(params Task<bool>[] tasks) => await (await Task.WhenAny(tasks).ConfigureAwait(false)).ConfigureAwait(false);

        private async Task<bool> getConfirmTimeout()
        {
            await Task.Delay(200).ConfigureAwait(false);
            return false;
        }

        private Task<bool> WaitForResponse(byte seq)
        {

            var tsc = new TaskCompletionSource<bool>();
            responseTasks[seq] = tsc;
            return tsc.Task;
        }

        private byte[] getBytes<T>(byte seq, T str, out ushort crc)
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size + 1 + 4 + 2];
            if (size > byte.MaxValue) throw new InvalidOperationException($"Can't send payloads bigger than {byte.MaxValue}");
            arr[0] = (byte)'M';
            arr[1] = (byte)'S';
            arr[2] = (byte)'G';
            arr[3] = (byte)size;
            arr[4] = seq;
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 5, size);
            Marshal.FreeHGlobal(ptr);
            crc = computeChecksum(arr, 5, arr.Length - 7);/* length - seq - header - crc */

            arr[arr.Length - 2] = (byte)crc;
            arr[arr.Length - 1] = (byte)(crc >> 8);
            return arr;
        }

        private ushort computeChecksum(byte[] arr, int start, int len)
        {
            ushort crc = 0xFFFF;

            for (int pos = start; pos < len + start; pos++)
            {
                crc ^= arr[pos];   // XOR byte into least sig. byte of crc

                for (int i = 8; i != 0; i--)
                {    // Loop over each bit
                    if ((crc & 0x0001) != 0)
                    {      // If the LSB is set
                        crc >>= 1;                    // Shift right and XOR 0xA001
                        crc ^= 0xA001;
                    }
                    else                            // Else LSB is not set
                        crc >>= 1;                    // Just shift right
                }
            }
            // Note, this number has low and high bytes swapped, so use it accordingly (or swap bytes)
            return crc;

        }

        private T getStruct<T>(byte[] bytes, int offset)
            where T : new()
        {
            T str = new T();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(bytes, offset, ptr, size);

            str = (T)Marshal.PtrToStructure(ptr, str.GetType());
            Marshal.FreeHGlobal(ptr);

            return str;

        }

        public CommResult ResetAsync()
        {

            var payload = new RequestHeader
            {
                Id = getID(),
                Type = MessageType.Clear
            };
            return SendWithResult(payload, payload.Id);
        }


        private CommResult SendWithResult<T>(T payload, uint id)
            where T : struct
        {
            var seq = sequence++;
            var cancel = cancelCommands;
            var tcsAck = getTask(id, taskAcknowledgements);
            var tcsCmp = getTask(id, taskCompletions);
            var result = WriteAsync(seq, payload, cancel.Token);

            return CommResult.Create(id, result, tcsAck, tcsCmp, cancel.Token);
        }

        public CommResult ClearAsync()
        {

            var payload = new RequestHeader
            {
                Id = getID(),
                Type = MessageType.Clear
            };
            return SendWithResult(payload, payload.Id);
        }


        public CommResult WritePositionAsync(Position pos)
        {
            var payload = new PositionDatagram
            {
                Header = new RequestHeader
                {
                    Id = getID(),
                    Type = MessageType.Clear
                },
                Position = pos
            };
            return SendWithResult(payload, payload.Header.Id);
        }

        private uint getID()
        {
            return id++;
        }
        private TaskCompletionSource<ulong> getTask(ulong commandId, Dictionary<ulong, TaskCompletionSource<ulong>> source)
        {
            var tcs = new TaskCompletionSource<ulong>();
            lock (source)
                source.Add(commandId, tcs);
            return tcs;
        }

    }
}
