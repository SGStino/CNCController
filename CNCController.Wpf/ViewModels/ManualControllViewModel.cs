using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Wpf.ViewModels
{
    public class ManualControllViewModel
    {
        private readonly Communications comms;
        private int distanceX;
        private int distanceY;
        private int distanceZ;
        private int speedX;
        private int speedY;
        private int speedZ;
        public ManualControllViewModel(Communications comms, IObservable<bool> isOpen)
        {
            this.comms = comms; 
            MoveBackwardCommand = ReactiveCommand.CreateFromTask(() => moveX(distanceY, speedY), isOpen);
            MoveForwardCommand = ReactiveCommand.CreateFromTask(() => moveX(-distanceY, speedY), isOpen);
            MoveLeftCommand = ReactiveCommand.CreateFromTask(() => moveY(-distanceX, speedX), isOpen);
            MoveRightCommand = ReactiveCommand.CreateFromTask(() => moveY(distanceX, speedX), isOpen);
            MoveUpCommand = ReactiveCommand.CreateFromTask(() => moveZ(distanceZ, speedZ), isOpen);
            MoveDownCommand = ReactiveCommand.CreateFromTask(() => moveZ(-distanceZ, speedZ), isOpen);
        }

        private Task moveX(int distanceX, int speedX)
        {
            return Task.Delay(1000);
        }
        private Task moveY(int distanceX, int speedX)
        {
            return Task.Delay(1000);
        }
        private Task moveZ(int distanceX, int speedX)
        {
            return Task.Delay(1000);
        }

        public ReactiveCommand MoveBackwardCommand { get; }
        public ReactiveCommand MoveForwardCommand { get; }
        public ReactiveCommand MoveLeftCommand { get; }
        public ReactiveCommand MoveRightCommand { get; }
        public ReactiveCommand MoveUpCommand { get; }
        public ReactiveCommand MoveDownCommand { get; }
    }
}
