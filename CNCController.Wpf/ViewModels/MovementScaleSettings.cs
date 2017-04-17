using System;
using System.Collections.Generic;
using System.Linq; 
using System.Threading.Tasks;

namespace CNCController.Wpf.ViewModels
{
    public class MovementScaleSettings
    {
        public int XAxisSteps = 7500;
        public double XAxisLength = 31.8;
        public int YAxisSteps = 12000;
        public double YAxisLength = 25.3;
        public int ZAxisSteps = 96000;
        public double ZAxisLength = 23.6;

        public int XSteps(double cm) => (int)(cm * XAxisSteps / XAxisLength);
        public int YSteps(double cm) => (int)(cm * YAxisSteps / YAxisLength);
        public int ZSteps(double cm) => (int)(cm * ZAxisSteps / YAxisLength);


        public double XDistance(int steps) => (steps * XAxisLength / XAxisSteps);
        public double YDistance(int steps) => (steps * YAxisLength / YAxisSteps);
        public double ZDistance(int steps) => (steps * ZAxisLength / ZAxisSteps);
    }
}
