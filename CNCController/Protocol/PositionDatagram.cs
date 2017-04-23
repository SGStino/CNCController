using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public struct PositionDatagram
    {
        public const int SIZE = RequestHeader.SIZE + Movement.SIZE;
        [FieldOffset(0)]
        public RequestHeader Header;
        [FieldOffset(RequestHeader.SIZE)]
        public Movement Position;
    }
}
