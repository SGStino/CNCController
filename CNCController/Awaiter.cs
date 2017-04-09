using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController
{
    public class Awaiter
    {
        private volatile TaskCompletionSource<byte> _waiting;
        public void Pulse()
        {
            var w = _waiting;
            _waiting = null;
            w?.TrySetResult(1);
        }

        public Task WaitAsync() => (_waiting ?? (_waiting = new TaskCompletionSource<byte>())).Task;
    }
}
