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
        private const int QUEUE_SIZE = 20;
        private SemaphoreSlim queueSizeSemaphore = new SemaphoreSlim(QUEUE_SIZE);

        private const byte HEADER_SIZE = 4;
        private byte[] header = new byte[] { (byte)'M', (byte)'S', (byte)'G' };

        private CancellationTokenSource cancelCommands = new CancellationTokenSource();
        private SerialPort serial;
        private uint id = 0;
        private readonly Dictionary<uint, TaskCompletionSource<uint>> taskAcknowledgements = new Dictionary<uint, TaskCompletionSource<uint>>();
        private readonly Dictionary<uint, TaskCompletionSource<uint>> taskCompletions = new Dictionary<uint, TaskCompletionSource<uint>>();

        public event Action<byte[], int, int> RawDataReceived;
        public event Action<byte[], int, int> RawDataSent;
        public event Action<bool> ConnectionChanged;
        public event Action<Position> PositionConfirmed;

        private byte sequence = 0;

        private TaskCompletionSource<bool>[] responseTasks = new TaskCompletionSource<bool>[255];

        public async void Open(string port, int baudrate = 9600)
        {
            serial = new SerialPort(port, baudrate);
            serial.Open();
            ConnectionChanged?.Invoke(true);
            await doReading();
            ConnectionChanged?.Invoke(false);
        }
        public void Close()
        {
            serial.Close();
        }

        private async Task doReading()
        {
            try
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
            catch (Exception e)
            {
            }
        }

        private void onReceived(byte[] buffer, byte payloadStart, byte length, byte seq)
        {
            this.RawDataReceived?.Invoke(buffer, payloadStart, length);
            if (checkAck(buffer, payloadStart, length, out bool ack, out byte ackSeq))
                confirm(ackSeq, ack);
            else
            {
                var header = Encoding.ASCII.GetString(buffer, payloadStart, 3);
                switch (header)
                {
                    case "STA":
                        if (length >= Marshal.SizeOf<CommandResponse>() + 3)
                            onStart(getStruct<CommandResponse>(buffer, payloadStart + 3));
                        break;
                    case "STO":
                        if (length >= Marshal.SizeOf<CommandResponse>() + 3)
                            onStop(getStruct<CommandResponse>(buffer, payloadStart + 3));
                        break;
                }
            }
        }

        private void onStop(CommandResponse rsp)
        {
            var id = rsp.Id;
            acknowledge(id);
            complete(id);
            ensureQueueSize(rsp);
            PositionConfirmed?.Invoke(getPosition(rsp));
        }

        private Position getPosition(CommandResponse rsp)
        {
            return new Position
            {
                X = rsp.X,
                Y = rsp.Y,
                Z = rsp.Z,
                E = rsp.E
            };
        }

        private void ensureQueueSize(CommandResponse rsp)
        {
            int queueReleased = (QUEUE_SIZE - queueSizeSemaphore.CurrentCount) - rsp.QueueLength;
            if (queueReleased > 0)
                queueSizeSemaphore.Release(queueReleased);
        }

        private void onStart(CommandResponse rsp)
        {
            var previous = taskAcknowledgements.Keys.Where(k => k < rsp.Id).ToArray();
            ensureQueueSize(rsp);
            acknowledge(rsp.Id);
            foreach (var id in previous)
            {
                acknowledge(id);
                complete(id);
            }
            PositionConfirmed?.Invoke(getPosition(rsp));
        }

        private void acknowledge(uint id)
        {
            if (taskAcknowledgements.TryGetValue(id, out TaskCompletionSource<uint> tcs))
                tcs.TrySetResult(id);
        }
        private void complete(uint id)
        {
            if (taskCompletions.TryGetValue(id, out TaskCompletionSource<uint> tcs))
                tcs.TrySetResult(id);
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



        private SemaphoreSlim writeSemaphore = new SemaphoreSlim(1);

        public async Task WriteAsync<T>(byte seq, T datagram, CancellationToken cancellationToken)
            where T : struct
        {
            await queueSizeSemaphore.WaitAsync().ConfigureAwait(false);
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
            await Task.Delay(1000).ConfigureAwait(false);
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
            var test = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 5, size);
            Marshal.Copy(ptr, test, 0, size);
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
                Type = MessageType.Reset
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
            var result = SendWithResult(payload, payload.Id);


            result.Send.ContinueWith(t =>
            {
                queueSizeSemaphore.Release(QUEUE_SIZE - queueSizeSemaphore.CurrentCount);
                acknowledgeAll();
                completeAll();
            }); // auto complete clear command as no start/stop get send

            return result;
        }

        private void completeAll()
        {
            setAllResults(taskCompletions);
        }

        private void acknowledgeAll()
        {
            setAllResults(taskAcknowledgements);
        }

        private static void setAllResults(Dictionary<uint, TaskCompletionSource<uint>> dict)
        {
            var tasks = dict.ToArray();
            dict.Clear();
            foreach (var task in tasks)
                task.Value.TrySetResult(task.Key);
        }

        public CommResult WritePositionAsync(Movement pos)
        {
            var payload = new PositionDatagram
            {
                Header = new RequestHeader
                {
                    Id = getID(),
                    Type = MessageType.Position
                },
                Position = pos
            };
            return SendWithResult(payload, payload.Header.Id);
        }

        private uint getID()
        {
            return id++;
        }
        private TaskCompletionSource<uint> getTask(uint commandId, Dictionary<uint, TaskCompletionSource<uint>> source)
        {
            var tcs = new TaskCompletionSource<uint>();
            lock (source)
                source.Add(commandId, tcs);
            return tcs;
        }

    }
}
