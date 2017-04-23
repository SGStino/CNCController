using CNCController.Protocol;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Wpf.ViewModels
{
    public class ManualControllViewModel : ReactiveObject
    {
        private readonly MovementScaleSettings scale;
        private readonly Communications comms;
        private double distanceX = .5;
        private double distanceY = .5;
        private double distanceZ = .5;
        private double speed = 0.5; // cm/s

        private bool movingUp;
        private bool movingDown;
        private bool movingLeft;
        private bool movingRight;
        private bool movingForward;
        private bool movingBackward;

        public event Action<string> StatusChanged;

        public ManualControllViewModel(MovementScaleSettings scale, Communications comms, IObservable<bool> isOpen)
        {
            this.scale = scale;
            this.comms = comms;

        }

        private bool isMoving = false;

        public bool MovingUp { get => movingUp; set { if (this.RaiseAndSetIfChanged(ref movingUp, value)) updateMovementAsync(); } }
        public bool MovingDown { get => movingDown; set { if (this.RaiseAndSetIfChanged(ref movingDown, value)) updateMovementAsync(); } }
        public bool MovingLeft { get => movingLeft; set { if (this.RaiseAndSetIfChanged(ref movingLeft, value)) updateMovementAsync(); } }
        public bool MovingRight { get => movingRight; set { if (this.RaiseAndSetIfChanged(ref movingRight, value)) updateMovementAsync(); } }
        public bool MovingForward { get => movingForward; set { if (this.RaiseAndSetIfChanged(ref movingForward, value)) updateMovementAsync(); } }
        public bool MovingBackward { get => movingBackward; set { if (this.RaiseAndSetIfChanged(ref movingBackward, value)) updateMovementAsync(); } }

        private async void updateMovementAsync()
        {
            if (!isMoving)
            {
                isMoving = true;
                do
                {
                    await sendMovement();
                } while ((movingUp ^ movingDown) || (movingRight ^ movingLeft) || (movingForward ^ movingBackward));
                isMoving = false;
                StatusChanged?.Invoke("Stopped moving");
            }
        }

        private async Task sendMovement()
        {

            double mY = movingLeft ? -distanceY : movingRight ? distanceY : 0;
            double mZ = movingUp ? -distanceZ : movingDown ? distanceZ : 0;
            double mX = movingForward ? distanceX : movingBackward ? -distanceX : 0;
            var d = Math.Sqrt(mX * mX + mY * mY + mZ * mZ);

            var duration = (uint)(d / speed * 1000000);
            var pos = new Movement
            {
                Duration = duration,
                StepY = scale.YSteps(mY),
                StepZ = scale.ZSteps(mZ),
                StepX = scale.XSteps(mX),
                Flags = MovementFlags.RelativeX | MovementFlags.RelativeY | MovementFlags.RelativeZ
            };


            var send = comms.WritePositionAsync(pos);
            StatusChanged?.Invoke($"{pos.StepX} {pos.StepY} {pos.StepZ} in {duration / 1000} ms");
            try
            {
                await send.Confirmed;
                await send.Confirmed;
                await send.Completed;
            }
            catch (Exception e)
            {

            }
        }
    }
}
