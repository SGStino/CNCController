using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CNCController
{
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public struct RequestHeader
    {
        public const int SIZE = sizeof(MessageType) + sizeof(ulong);
        [FieldOffset(0)]
        public MessageType Type;
        [FieldOffset(sizeof(MessageType))]
        public ulong Id;
    }
}
