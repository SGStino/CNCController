using System;

namespace CNCController
{
    [Flags]
    public enum PositionFlags : byte
    {
        RelativeX = 1,
        RelativeY = 2,
        RelativeZ = 4,
        AbsoluteE = 8
    }
}