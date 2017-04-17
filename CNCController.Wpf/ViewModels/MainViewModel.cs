using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.IO.Ports;
using System.Collections.ObjectModel;
using CNCController.Protocol;

namespace CNCController.Wpf.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        private Communications comms = new Communications();
        private string comPort;
        private string status;
        private bool isOpen;
        private readonly MovementScaleSettings scale;

        public ManualControllViewModel ManualControl { get; }
        public Milling2DControlViewModel Milling2DControl { get; }

        public bool IsOpen { get => isOpen; set => this.RaiseAndSetIfChanged(ref isOpen, value); }
        public string Status { get => status; set => this.RaiseAndSetIfChanged(ref status, value); }

        public string ComPort { get => comPort; set => this.RaiseAndSetIfChanged(ref comPort, value); }
        public ReactiveCommand StartCommand { get; }
        public ReactiveCommand StopCommand { get; }
        public ReactiveCommand ResetCommand { get; }
        public ReactiveCommand ClearCommand { get; }

        public ReadOnlyObservableCollection<string> ComPorts => DeviceChangeEvent.ComPorts;

        public ReactiveList<string> RawInput { get; } = new ReactiveList<string>();
        public ReactiveList<string> RawOutput { get; } = new ReactiveList<string>();
        public ReactiveList<ResponseViewModel> Responses { get; } = new ReactiveList<ResponseViewModel>();

        public MainViewModel()
        {
            this.scale = new MovementScaleSettings();
            comPort = SerialPort.GetPortNames().FirstOrDefault();
            var comportSet = this.WhenAny(x => x.ComPort, x => !string.IsNullOrWhiteSpace(x.Value));


            var inputStream = Observable.FromEvent<Action<byte[], int, int>, byte[]>(handler =>
               {
                   Action<byte[], int, int> h = (arg1, arg2, arg3) => handler(FormatBytes(arg1, arg2, arg3));
                   return h;
               }
            , handler => comms.RawDataReceived += handler, handler => comms.RawDataReceived -= handler).SelectMany(m => m);

            var outputStream = Observable.FromEvent<Action<byte[], int, int>, byte[]>(handler =>
            {
                Action<byte[], int, int> h = (arg1, arg2, arg3) => handler(FormatBytes(arg1, arg2, arg3));
                return h;
            }
            , handler => comms.RawDataSend += handler, handler => comms.RawDataSend -= handler).SelectMany(m => m);

            var responseStream = Observable.FromEvent<Response>(h => comms.ResponseReceived += h, h => comms.ResponseReceived -= h);

            var isOpen = Observable.FromEvent<bool>(handler => comms.ConnectionChanged += handler, handler => comms.ConnectionChanged -= handler, RxApp.MainThreadScheduler);
            isOpen = isOpen.StartWith(false);
            var isNotOpen = isOpen.Select(i => !i);

            StartCommand = ReactiveCommand.Create(() => comms.Open(ComPort), isNotOpen);
            StopCommand = ReactiveCommand.Create(() => comms.Close(), isOpen);

            isOpen.Subscribe(i => this.IsOpen = i);


            ResetCommand = ReactiveCommand.CreateFromTask(() => reset(), isOpen);
            ClearCommand = ReactiveCommand.CreateFromTask(() => clear(), isOpen);


            inputStream.ObserveOn(RxApp.MainThreadScheduler).Subscribe(m => RawInput.Add(string.Format("{0:X2}", m)));
            outputStream.ObserveOn(RxApp.MainThreadScheduler).Subscribe(m => RawOutput.Add(string.Format("{0:X2}", m)));
            responseStream.ObserveOn(RxApp.MainThreadScheduler).Subscribe(m => Responses.Add(new ResponseViewModel(m)));

            this.ManualControl = new ManualControllViewModel(scale, comms, isOpen);
            this.ManualControl.StatusChanged += ManualControl_StatusChanged;
            this.Milling2DControl = new Milling2DControlViewModel(scale, comms, isOpen);
        }

        private void ManualControl_StatusChanged(string status) => Status = status;

        private async Task clear()
        {
            var result = comms.ClearAsync();
            this.Responses.Clear();
            this.RawInput.Clear();
            this.RawOutput.Clear();
            await waitForCommandStatus(result, "Clear");
            
        }

        private byte[] FormatBytes(byte[] arg1, int arg2, int arg3) => arg1.Skip(arg2).Take(arg3).ToArray();

        private async Task reset()
        {
            var result = comms.ResetAsync();

            await waitForCommandStatus(result, "Reset");

        }

        private async Task waitForCommandStatus(CommResult result, string command)
        {
            Status = await getStatus(result.Send, command, "Sent");
            Status = await getStatus(result.Confirmed, command, "Confirmed");
            Status = await getStatus(result.Completed, command, "Completed");
        }

        private async Task<string> getStatus(Task completed, string command, string status)
        {
            try
            {
                await completed;
                return $"{command} {status}";
            }
            catch (OperationCanceledException)
            {
                return $"{command} {status} Cancelled";
            }
            catch (Exception e)
            {
                return $"{command} {status} Failed: {e.Message}";
            }
        }
    }
}
