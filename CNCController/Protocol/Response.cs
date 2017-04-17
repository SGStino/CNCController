using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public struct Response
    {
        public const int SIZE = sizeof(ResponseType) + RequestHeader.SIZE + 2 * sizeof(byte);
        [FieldOffset(0)]
        public ResponseType Type;

        [FieldOffset(sizeof(ResponseType))]
        public byte QueueLength;
        [FieldOffset(sizeof(ResponseType) + sizeof(byte))]
        public byte QueueAvailable;

        [FieldOffset(sizeof(ResponseType) + sizeof(byte) * 2)]
        public RequestHeader Header;
    }
}
