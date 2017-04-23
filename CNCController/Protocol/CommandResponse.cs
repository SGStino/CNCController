using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public struct CommandResponse
    {
        public const int SIZE = sizeof(int) * 3 + sizeof(int) + sizeof(long);
        [FieldOffset(0)]
        public uint Id;
        [FieldOffset(sizeof(int))]
        public uint X;
        [FieldOffset(sizeof(int)*2)]
        public uint Y;
        [FieldOffset(sizeof(int) * 3)]
        public uint Z;
        [FieldOffset(sizeof(int) * 4)]
        public long E;
        [FieldOffset(sizeof(int) * 4 + sizeof(long))]
        public byte QueueLength;
    }
}
