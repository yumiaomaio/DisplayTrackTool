// File: Interop/Structs/MONITORINFO.cs

using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct Monitorinfo
{
    public int cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;
}