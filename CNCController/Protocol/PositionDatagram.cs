using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Protocol
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PositionDatagram
    {
        public RequestHeader Header;
        public Position Position;
    }
}
