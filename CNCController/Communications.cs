using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CNCController
{
    public class Communications
    {
        private CancellationTokenSource cancelCommands = new CancellationTokenSource();
        public static readonly byte[] PREFIX = new byte[] { (byte)'M', (byte)'S', (byte)'G' };

        private object syncRoot = new object();
        private readonly SerialPort serial;
        private ulong id;

        private int queueAvailable = 1;


        public Communications(string port, int baudrate)
        {
            this.serial = new SerialPort(port, baudrate);

        }

        public void Open()
        {
            serial.Open();
            beginReading();
        }



        public event Action<byte[], int, int> RawDataReceived;
        public event Action<byte[], int, int> RawDataSend;
        public event Action<byte[], int, int> RawResponseReceived;
        public event Action<Response> ResponseReceived;

        private async void beginReading()
        {
            var messageSize = Marshal.SizeOf<Response>();
            byte[] buffer = new byte[messageSize + 3];
            int offset = 0;
            do
            {
                int count = await serial.BaseStream.ReadAsync(buffer, offset, messageSize - offset).ConfigureAwait(false);
                RawDataReceived?.Invoke(buffer, offset, count);
                offset += count;

                if (offset > 3)
                {
                    byte msgPos;
                    if (findMsg(buffer, out msgPos) && msgPos != 0)
                    {
                        shiftLeft(buffer, msgPos);
                        offset -= msgPos;
                    }
                }

                if (offset < messageSize)
                    continue;

                offset = 0; // next message

                RawResponseReceived?.Invoke(buffer, 3, messageSize + 3);

                //throw new InvalidOperationException("Invalid message: " + ASCIIEncoding.ASCII.GetString(buffer, 0, count) + " "+ string.Join(" ",buffer.Take(count).Select(v => string.Format("{0:X}", v))));
                var response = getStruct<Response>(buffer, 3);
                ResponseReceived?.Invoke(response);
                lock (syncRoot)
                {
                    queueAvailable = response.QueueAvailable;
                    awaiter.Pulse();
                }
                switch (response.Type)
                {
                    case ResponseType.Completed:
                    case ResponseType.Acknowledge:
                        completeAcknowledgement(response.Header, false, response.Type == ResponseType.Completed);
                        break;
                    case ResponseType.Error:
                        completeAcknowledgement(response.Header, true, true);
                        break;
                    case ResponseType.Startup:
                        // startupEvent;
                        break;
                }

            } while (serial.IsOpen);
        }

        private void shiftLeft(byte[] buffer, byte msgPos)
        {
            for (int i = msgPos; i < buffer.Length; i++)
                buffer[i - msgPos] = buffer[i];
        }

        public static bool findMsg(byte[] buffer, out byte pos)
        {
            bool found;
            for (byte i = 0; i < buffer.Length - PREFIX.Length; i++)
            {
                pos = i;
                found = true;
                for (int j = 0; j < PREFIX.Length; j++)
                {
                    if (buffer[i + j] != PREFIX[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return true;
            }
            pos = default(byte);
            return false;
        }

        private void completeAcknowledgement(RequestHeader header, bool error, bool completed)
        {
            TaskCompletionSource<ulong> tcs;
            lock (taskAcknowledgements)
                if (taskAcknowledgements.TryGetValue(header.Id, out tcs))
                {
                    if (error)
                        tcs.SetException(new Exception("Error"));
                    else
                        tcs.SetResult(header.Id);
                    taskAcknowledgements.Remove(header.Id);
                }
            if (completed)
            {
                lock (taskCompletions)
                    if (taskCompletions.TryGetValue(header.Id, out tcs))
                    {
                        if (error)
                            tcs.SetException(new Exception("Error"));
                        else
                            tcs.SetResult(header.Id);
                        taskCompletions.Remove(header.Id);
                    }
            }
        }

        public void Close()
        {
            serial.Close();
        }


        private Dictionary<ulong, TaskCompletionSource<ulong>> taskAcknowledgements = new Dictionary<ulong, TaskCompletionSource<ulong>>();
        private Dictionary<ulong, TaskCompletionSource<ulong>> taskCompletions = new Dictionary<ulong, TaskCompletionSource<ulong>>();

        public CommResult ResetAsync()
        {
            var commandId = getID();
            byte[] request = getBytes(new RequestHeader
            {
                Id = commandId,
                Type = MessageType.Reset
            });
            var cancel = cancelCommands;
            var tcsAck = getTask(commandId, taskAcknowledgements);
            var tcsCmp = getTask(commandId, taskCompletions);
            var send = sendAsync(request, cancel.Token);

            return CommResult.Create(commandId, send, tcsAck, tcsCmp, cancelCommands.Token);
        }



        private SemaphoreSlim sendSemaphore = new SemaphoreSlim(1);
        private SemaphoreSlim sendVerifySemaphore = new SemaphoreSlim(1);
        private Awaiter awaiter = new Awaiter();
        private async Task sendAsync(byte[] request, CancellationToken cancel, bool force = false)
        { // only one command can send at a time 
            if (!force) // when forced (like for clear), the command gets send anyway
            {
                await sendVerifySemaphore.WaitAsync();
                lock (syncRoot)
                {
                    queueAvailable--;
                }

                while (queueAvailable < 0 && !cancel.IsCancellationRequested) // 0 means this one was the last to go into the queue
                {
                    await awaiter.WaitAsync();
                }
                sendVerifySemaphore.Release();
            }

            cancel.ThrowIfCancellationRequested();

            await sendSemaphore.WaitAsync();
            await WriteAsync(PREFIX, 0, PREFIX.Length);
            await WriteAsync(request, 0, request.Length).ConfigureAwait(false);
            sendSemaphore.Release();

        }

        public CommResult ClearAsync()
        {
            cancelCommands?.Cancel(); // any pending commands get cancelled
            cancelCommands = new CancellationTokenSource();
            var commandId = getID();
            byte[] request = getBytes(new RequestHeader
            {
                Id = commandId,
                Type = MessageType.Clear
            });
            var cancel = cancelCommands.Token;
            var tcsAck = getTask(commandId, taskAcknowledgements);
            var send = sendAsync(request, cancel, true);

            return CommResult.Create(commandId, send, tcsAck, tcsAck, cancel); // this is complete as soon as it's confirmed
        }

        private TaskCompletionSource<ulong> getTask(ulong commandId, Dictionary<ulong, TaskCompletionSource<ulong>> source)
        {
            var tcs = new TaskCompletionSource<ulong>();
            lock (source)
                source.Add(commandId, tcs);
            return tcs;
        }

        public CommResult WritePositionAsync(Position position)
        {
            var commandId = getID();
            var requestHeader = getBytes(new RequestHeader
            {
                Id = commandId,
                Type = MessageType.Position
            });
            var cancel = cancelCommands.Token;
            var requestPosition = getBytes(position);
            var request = combine(requestHeader, requestPosition);
            var tcsAck = getTask(commandId, taskAcknowledgements);
            var tcsCmp = getTask(commandId, taskCompletions);
            var send = sendAsync(request, cancel);

            return CommResult.Create(commandId, send, tcsAck, tcsCmp, cancel);
        }

        private Task WriteAsync(byte[] data, int offset, int length)
        {
            RawDataSend?.Invoke(data, offset, length);
            return serial.BaseStream.WriteAsync(data, offset, length);
        }

        private byte[] combine(params byte[][] arrays)
        {
            int count = arrays.Sum(a => a.Length);
            var result = new byte[count];
            var offset = 0;
            foreach (var ar in arrays)
            {
                Array.Copy(ar, 0, result, offset, ar.Length);
                offset += ar.Length;
            }
            return result;
        }

        private ulong getID()
        {
            lock (syncRoot)
                return id++;
        }

        private byte[] getBytes<T>(T str)
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
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
    }
}
