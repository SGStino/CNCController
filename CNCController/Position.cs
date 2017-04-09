using System.Runtime.InteropServices;

namespace CNCController
{
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public struct Position
    {
        public const int SIZE = sizeof(int) * 3 + sizeof(long) + sizeof(uint) + sizeof(PositionFlags);
        [FieldOffset(0)]
        public PositionFlags Flags;
        [FieldOffset(sizeof(PositionFlags))]
        public int StepX;
        [FieldOffset(sizeof(PositionFlags) + sizeof(int))]
        public int StepY;
        [FieldOffset(sizeof(PositionFlags) + sizeof(int) * 2)]
        public int StepZ;
        [FieldOffset(sizeof(PositionFlags) + sizeof(int) * 3)]
        public long StepE;
        [FieldOffset(sizeof(PositionFlags) + sizeof(int) * 3 + sizeof(long))]
        public uint Duration;
    }
}