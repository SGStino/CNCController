using System;

namespace CNCController.Protocol
{
    [Flags]
    public enum MovementFlags : byte
    {
        RelativeX = 1,
        RelativeY = 2,
        RelativeZ = 4,
        AbsoluteE = 8
    }
}