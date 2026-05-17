using System.Runtime.InteropServices;

namespace ImmersiveWindow.Interop.Structs;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MonitorinfoEx
{
    public int cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice;
}
