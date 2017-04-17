using CNCController;
using CNCController.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Communications comms;

        public MainWindow()
        {
            InitializeComponent();
            this.comms = new Communications();//230400
            Start();
            comms.RawDataReceived += Comms_RawDataReceived1;
            comms.RawDataSend += Comms_RawDataReceived2;
            //comms.RawResponseReceived += Comms_RawDataReceived;
            //comms.ResponseReceived += Comms_ResponseReceived;
            Input.Text = "INPUT" + Environment.NewLine;
            Output.Text = "OUTPUT" + Environment.NewLine;
        }

        private void Comms_RawDataReceived2(byte[] buff, int offset, int count)
        {
            var data = string.Join(" ", buff.Skip(offset).Take(count).Select(v => string.Format("{0:x2} '{1}'", v, format((char)v))));

            Dispatcher.Invoke(() => this.LogInput(data + " "));
        }

        private void Comms_RawDataReceived1(byte[] buff, int offset, int count)
        {
            var data = string.Join(" ", buff.Skip(offset).Take(count).Select(v => string.Format("{0:x2} '{1}'", v, format((char)v))));

            Dispatcher.Invoke(() => this.LogOutput(data + " "));
        }

        private char format(char v)
        {
            return char.IsLetterOrDigit(v) ? v : '.';
        }

        private void Comms_ResponseReceived(Response obj)
        {
            Dispatcher.Invoke(() => this.Log($"{obj.Type} {obj.Header.Type} {obj.Header.Id}"));
        }

        private void Comms_RawDataReceived(byte[] arg1, int offset, int arg2)
        {
            var data = string.Join(" ", arg1.Skip(offset).Take(arg2).Select(v => string.Format("{0:x2}", v)));

            Dispatcher.Invoke(() => this.Log(data));
        }

        public void Start()
        {
            comms.Open("COM5", 9600);
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = comms.ResetAsync(); ;

                debugCommand("Reset", result);
            }
            catch (Exception ex)
            {
                this.Log($"Reset Error: {ex.Message}");
            }
        }

        private async Task debugCommand(string v, CommResult result)
        {
            this.Log($"{v} {result.Id}");
            await debugCommand(v, "Send", result.Send, result.Id);
            await debugCommand(v, "Confirmed", result.Confirmed, result.Id);
            await debugCommand(v, "Completed", result.Completed, result.Id);
        }

        private async Task debugCommand(string command, string state, Task task, ulong id)
        {
            try
            {
                await task;
                this.Log($"{command} {id} {state}");
            }
            catch (OperationCanceledException)
            {
                this.Log($"{command} {id} {state} Cancelled");
            }
            catch (Exception ex)
            {
                this.Log($"{command} {id} {state} Failed: {ex.Message}");
            }
        }

        private void SplitLogButton_Click(object sender, RoutedEventArgs e)
        {
            Output.Text += Environment.NewLine;
            Input.Text += Environment.NewLine;
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = comms.ClearAsync(); ;

                debugCommand("Clear", result);
            }
            catch (Exception ex)
            {
                this.Log($"Clear Error: {ex.Message}");
            }
        }

        private void Log(string v)
        {
            list.Items.Insert(0, v);
        }

        private void LogInput(string v)
        {
            Input.Text += v + Environment.NewLine;
        }

        private void LogOutput(string v)
        {
            Output.Text += v + Environment.NewLine;
        }

        private async void MoveButton_Click(object sender, RoutedEventArgs args)
        {
            int x, y, z;
            long e;
            uint t;

            PositionFlags flags = 0;

            if (CB_X_Relative.IsChecked ?? false)
                flags |= PositionFlags.RelativeX;
            if (CB_Y_Relative.IsChecked ?? false)
                flags |= PositionFlags.RelativeY;
            if (CB_Z_Relative.IsChecked ?? false)
                flags |= PositionFlags.RelativeZ;
            if (!(CB_E_Relative.IsChecked ?? false))
                flags |= PositionFlags.AbsoluteE;


            if (int.TryParse(TB_X.Text, out x) && int.TryParse(TB_Y.Text, out y) && int.TryParse(TB_Z.Text, out z) && long.TryParse(TB_E.Text, out e) && uint.TryParse(TB_T.Text, out t))
            {
                var pos = new Position()
                {
                    Duration = t,
                    StepE = e,
                    StepX = x,
                    StepY = y,
                    StepZ = z,
                    Flags = flags,
                };
                var result = comms.WritePositionAsync(pos);
                await debugCommand($"Move {x},{y},{z} {e} in {t / 1000000.0} sec", result);
            }
        }
    }
}
