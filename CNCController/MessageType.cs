using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController
{
    public enum MessageType : short
    {
        Reset = 1,
        Position = 2,
        Clear = 3
    }
}
