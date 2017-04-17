using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Reactive;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace CNCController.Wpf.ViewModels
{
    public class Milling2DControlViewModel : ReactiveObject
    {
        private readonly Communications comms;
        private readonly IObservable<bool> isOpen;
        private readonly MovementScaleSettings scale;

        private double travelDepth = 1;
        private double workDepth = 0;
        private double workSpeed = 1; // cm/s;
        private double travelSpeed = 2; //cm/s
        private double verticalSpeed = 0.1; //cm/s;

        private Point lastPosition;
         

        public ObservableCollection<LineViewModel> Lines { get; } = new ObservableCollection<LineViewModel>();

        public Milling2DControlViewModel(MovementScaleSettings scale, Communications comms, IObservable<bool> isOpen)
        {
            this.scale = scale;
            this.comms = comms;
            this.isOpen = isOpen;
            this.MoveCommand = ReactiveCommand.Create<Point>(MoveTo);
            this.LineCommand = ReactiveCommand.Create<Point>(LineTo);
            var reset = Observable.FromEvent(m => comms.PositionReset += m, m => comms.PositionReset -= m);

            reset.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => Comms_PositionReset());
        }

        private void Comms_PositionReset()
        {
            this.lastPosition = new Point();
            this.Lines.Clear();
        }

        public ReactiveCommand<Point, Unit> MoveCommand { get; }
        public ReactiveCommand<Point, Unit> LineCommand { get; }

        private void LineTo(Point point)
        {
            var moveToTarget = new Position()
            {
                StepX = scale.XSteps(point.X),
                StepY = scale.YSteps(point.Y),
                StepZ = 0,
                Flags = PositionFlags.RelativeZ,
                Duration = calculateDuration(lastPosition, point, workSpeed)
            };
            var result = comms.WritePositionAsync(moveToTarget);

            Lines.Add(new LineViewModel(lastPosition, point, true, result));
            lastPosition = point;

        }

        private uint calculateDuration(double distance, double speed) => (uint)(1000000 * (distance / speed)); // cm / (cm/s) = s

        private uint calculateDuration(Point a, Point b, double speed) => calculateDuration((b - a).Length, speed);

        private void MoveTo(Point point)
        {
            var verticalDuration = calculateDuration(travelDepth - workDepth, verticalSpeed);
            var moveDown = new Position()
            {
                StepX = 0,
                StepY = 0,
                StepZ = scale.ZSteps(travelDepth),
                Flags = PositionFlags.RelativeX | PositionFlags.RelativeY,
                Duration = verticalDuration
            };
            var moveToTarget = new Position()
            {
                StepX = scale.XSteps(point.X),
                StepY = scale.YSteps(point.Y),
                StepZ = 0,
                Flags = PositionFlags.RelativeZ,
                Duration = calculateDuration(lastPosition, point, travelSpeed)
            };

            var moveUp = new Position()
            {
                StepX = 0,
                StepY = 0,
                StepZ = scale.ZSteps(workDepth),
                Flags = PositionFlags.RelativeX | PositionFlags.RelativeY,
                Duration = verticalDuration
            };

            var mdr = comms.WritePositionAsync(moveDown);
            var mtr = comms.WritePositionAsync(moveToTarget);
            var mur = comms.WritePositionAsync(moveUp); 
            Lines.Add(new LineViewModel(lastPosition, point, false, mdr, mtr, mur));
            lastPosition = point;
        }
    }
}
