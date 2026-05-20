using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MonitorinfoEx
{
    public int cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;

    public Char32Buffer szDevice;
}
