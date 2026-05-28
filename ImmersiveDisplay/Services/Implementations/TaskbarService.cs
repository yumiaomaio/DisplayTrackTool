using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services.Implementations;

public class TaskbarService
{
    private bool _originalAutoHide;

    public void CaptureOriginalState()
    {
        _originalAutoHide = IsAutoHideEnabled();
    }

    public bool IsAutoHideEnabled()
    {
        var appBarData = new Appbardata
        {
            cbSize = Marshal.SizeOf<Appbardata>(),
            hWnd = NativeMethods.FindWindow("Shell_TrayWnd", null)
        };

        var state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref appBarData);
        return (state & (uint)NativeMethods.ABS_AUTOHIDE) != 0;
    }

    public void SetAutoHide(bool enable)
    {
        var appBarData = new Appbardata
        {
            cbSize = Marshal.SizeOf<Appbardata>(),
            hWnd = NativeMethods.FindWindow("Shell_TrayWnd", null),
            lParam = (IntPtr)(enable ? NativeMethods.ABS_AUTOHIDE : NativeMethods.ABS_ALWAYSONTOP)
        };

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETSTATE, ref appBarData);
    }

    public void RestoreOriginalState()
    {
        SetAutoHide(_originalAutoHide);
    }
}
