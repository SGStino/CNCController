using System;
using System.Threading;
using System.Threading.Tasks;

namespace CNCController
{
    public class CommResult
    {
        private CommResult(uint id, Task send, Task confirmed, Task completed)
        {
            this.Id = id;
            this.Send = send;
            this.Confirmed = confirmed;
            this.Completed = completed;
        }

        public uint Id { get; }
        public Task Send { get; }
        public Task Completed { get; }
        public Task Confirmed { get; }

        internal static CommResult Create(uint commandId, Task send, TaskCompletionSource<uint> tcsAck, TaskCompletionSource<uint> tcsCmp, CancellationToken cancel)
        {
            send.ContinueWith(t => cascadeCancelFail(t, tcsAck));
            cancel.Register(() => tcsAck.TrySetCanceled());
            if (tcsAck != tcsCmp)
            {
                cancel.Register(() => tcsCmp.TrySetCanceled());
                tcsAck.Task.ContinueWith(t => cascadeCancelFail(t, tcsCmp));
            }
            return new CommResult(commandId, send, tcsAck.Task, tcsCmp.Task);
        }

        private static void cascadeCancelFail(Task t, TaskCompletionSource<uint> tcsAck)
        {
            if (t.IsCanceled)
                tcsAck.TrySetCanceled();
            if (t.IsFaulted)
                tcsAck.TrySetException(t.Exception);
        }
    }
}