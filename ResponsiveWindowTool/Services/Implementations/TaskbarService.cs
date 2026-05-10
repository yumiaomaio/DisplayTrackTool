using System.Runtime.InteropServices;
using ResponsiveWindowTool.Interop;

namespace ResponsiveWindowTool.Services.Implementations;

public class TaskbarService : ITaskbarService
{
    public bool IsAutoHideEnabled()
    {
        var data = new NativeMethods.APPBARDATA();
        data.cbSize = Marshal.SizeOf(typeof(NativeMethods.APPBARDATA));
        uint state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data);
        return (state & NativeMethods.ABS_AUTOHIDE) != 0;
    }

    public void SetAutoHide(bool enable)
    {
        var data = new NativeMethods.APPBARDATA();
        data.cbSize = Marshal.SizeOf(typeof(NativeMethods.APPBARDATA));
        data.hWnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
        data.lParam = (IntPtr)(enable ? NativeMethods.ABS_AUTOHIDE : NativeMethods.ABS_ALWAYSONTOP);
        
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETSTATE, ref data);
    }
}