using ReactiveUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CNCController.Wpf.ViewModels
{
    public class LineViewModel : ReactiveObject
    {
        public Point Start { get; }
        public Point End { get; }
        public bool IsWork { get; }
        private CommResult[] result;
        private bool isQueued;
        private bool isCompleted;
        private bool isFaulted;

        public bool IsQueued { get => isQueued; set => this.RaiseAndSetIfChanged(ref isQueued, value); }
        public bool IsCompleted { get => isCompleted; set => this.RaiseAndSetIfChanged(ref isCompleted, value); }
        public bool IsFaulted { get => isFaulted; set => this.RaiseAndSetIfChanged(ref isFaulted, value); }

        public LineViewModel(Point start, Point end, bool isWork, params CommResult[] result)
        {
            this.Start = start;
            this.End = end;
            this.IsWork = isWork;
            this.result = result;
            this.start();
        }

        public async void start()
        {
            var hasBeenQueued = result[0].Send;

            var hasBeenCompleted = Task.WhenAll(result.Select(l => l.Completed));
            try
            {
                await hasBeenQueued;
                IsQueued = true;
                await hasBeenCompleted;
                IsQueued = false;
                IsCompleted = true;
            }
            catch (Exception e)
            {
                IsFaulted = true;
            }
        }
    }
}