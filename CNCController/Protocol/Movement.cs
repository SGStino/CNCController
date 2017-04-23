using System.Runtime.InteropServices;

namespace CNCController.Protocol
{
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    public struct Movement
    {
        public const int SIZE = sizeof(int) * 3 + sizeof(long) + sizeof(uint) + sizeof(MovementFlags);
        [FieldOffset(0)]
        public MovementFlags Flags;
        [FieldOffset(sizeof(MovementFlags))]
        public int StepX;
        [FieldOffset(sizeof(MovementFlags) + sizeof(int))]
        public int StepY;
        [FieldOffset(sizeof(MovementFlags) + sizeof(int) * 2)]
        public int StepZ;
        [FieldOffset(sizeof(MovementFlags) + sizeof(int) * 3)]
        public long StepE;
        [FieldOffset(sizeof(MovementFlags) + sizeof(int) * 3 + sizeof(long))]
        public uint Duration;
    }
}