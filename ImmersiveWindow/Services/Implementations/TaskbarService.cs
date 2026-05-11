using System.Runtime.InteropServices;
using ImmersiveWindow.Interop;

namespace ImmersiveWindow.Services.Implementations;

public class TaskbarService : ITaskbarService
{
    public bool IsAutoHideEnabled()
    {
        var data = new NativeMethods.Appbardata
        {
            cbSize = Marshal.SizeOf(typeof(NativeMethods.Appbardata))
        };
        uint state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data);
        return (state & NativeMethods.ABS_AUTOHIDE) != 0;
    }

    public void SetAutoHide(bool enable)
    {
        var data = new NativeMethods.Appbardata
        {
            cbSize = Marshal.SizeOf(typeof(NativeMethods.Appbardata)),
            hWnd = NativeMethods.FindWindow("Shell_TrayWnd", null),
            lParam = (enable ? NativeMethods.ABS_AUTOHIDE : NativeMethods.ABS_ALWAYSONTOP)
        };

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETSTATE, ref data);
    }
}