using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows;
using System.Collections.ObjectModel;
using System.IO.Ports;

namespace CNCController.Wpf
{
    public static  class DeviceChangeEvent
    {
        private static int WM_DEVICECHANGE = 0x0219;

        public static event Action DeviceChange;

        internal static void Init(Window window)
        {
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source.AddHook(new HwndSourceHook(WndProc));
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
                comPorts.Add(port);
        }

        private static ObservableCollection<string> comPorts = new ObservableCollection<string>();

        public static ReadOnlyObservableCollection<string> ComPorts => new ReadOnlyObservableCollection<string>(comPorts);

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE) //WM_DEVICECHANGE
            {
                DeviceChange?.Invoke();
                var ports = SerialPort.GetPortNames(); 
                var oldPorts = ComPorts.Except(ports).ToArray();
                var newPorts = ports.Except(ComPorts).ToArray();

                foreach (var newPort in newPorts)
                    comPorts.Add(newPort);
                foreach (var oldPort in oldPorts)
                    comPorts.Remove(oldPort);
            }
            //  do stuff
            return IntPtr.Zero;
        }
    }
}
