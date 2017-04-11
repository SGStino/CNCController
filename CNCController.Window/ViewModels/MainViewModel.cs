using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;

namespace CNCController.Window.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        private Communications comms = new Communications();
        private string comPort;
        private bool isOpen;

        public ReactiveList<string> ComPorts { get; }


        public string ComPort { get => comPort; set => this.RaiseAndSetIfChanged(ref comPort, value); }
        public bool IsOpen { get => isOpen; set => this.RaiseAndSetIfChanged(ref isOpen, value); }
        public ReactiveCommand<Unit, Unit> StartCommand { get; }
        public ReactiveCommand<Unit, Unit> StopCommand { get; private set; }

        public MainViewModel()
        {
            var comportSet = this.WhenAny(x => x.ComPort, x => !string.IsNullOrWhiteSpace(x.Value));
            var isNotOpen = this.WhenAny(x => x.IsOpen, x => !x.Value);
            var isOpen = this.WhenAny(x => x.IsOpen, x => x.Value);


            StartCommand = ReactiveCommand.Create(() => comms.Open(ComPort), comportSet.Concat(isNotOpen));
            StopCommand = ReactiveCommand.Create(() => comms.Open(ComPort), isOpen);
        }

    }
}
