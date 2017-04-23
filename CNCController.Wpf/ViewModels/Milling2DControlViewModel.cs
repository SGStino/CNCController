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
using CNCController.Protocol;

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
            var position = Observable.FromEvent<Position>(m => comms.PositionConfirmed += m, m => comms.PositionConfirmed -= m);

            position.ObserveOn(RxApp.MainThreadScheduler).Subscribe(p => Comms_PositionReset(p));
        }

        private void Comms_PositionReset(Position p)
        {
            if (p.X == 0 && p.Y == 0 && p.Z == 0)
            { //reset
                if (lastPosition != new Point())
                {
                    this.lastPosition = new Point();
                    this.Lines.Clear();
                }
            }
            else
                CurrentPosition = new Point(scale.XDistance((int)p.X), scale.YDistance((int)p.Y));
        }

        public ReactiveCommand<Point, Unit> MoveCommand { get; }
        public ReactiveCommand<Point, Unit> LineCommand { get; }
        private Point currentPosition;
        public Point CurrentPosition { get => currentPosition; set => this.RaiseAndSetIfChanged(ref currentPosition, value); }

        private void LineTo(Point point)
        {
            var moveToTarget = new Movement()
            {
                StepX = scale.XSteps(point.X),
                StepY = scale.YSteps(point.Y),
                StepZ = 0,
                Flags = MovementFlags.RelativeZ,
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
            var moveDown = new Movement()
            {
                StepX = 0,
                StepY = 0,
                StepZ = scale.ZSteps(travelDepth),
                Flags = MovementFlags.RelativeX | MovementFlags.RelativeY,
                Duration = verticalDuration
            };
            var moveToTarget = new Movement()
            {
                StepX = scale.XSteps(point.X),
                StepY = scale.YSteps(point.Y),
                StepZ = 0,
                Flags = MovementFlags.RelativeZ,
                Duration = calculateDuration(lastPosition, point, travelSpeed)
            };

            var moveUp = new Movement()
            {
                StepX = 0,
                StepY = 0,
                StepZ = scale.ZSteps(workDepth),
                Flags = MovementFlags.RelativeX | MovementFlags.RelativeY,
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
