using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Controls;
namespace CNCController.Window
{
    public class DeviceChangeEvent
    {
        private static int WM_DEVICECHANGE = 0x0219;

        

        internal void Init(Window window)
        {
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source.AddHook(new HwndSourceHook(WndProc));

        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            //  do stuff
        }
    }
}
