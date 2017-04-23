using CNCController.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Wpf.ViewModels
{
    public class ResponseViewModel
    {
        private Position position;

        public ResponseViewModel(Position position)
        {
            this.position = position;
        }

        public uint X => position.X;
        public uint Y => position.Y;
        public uint Z => position.Z;
        public long E => position.E;
    }
}
