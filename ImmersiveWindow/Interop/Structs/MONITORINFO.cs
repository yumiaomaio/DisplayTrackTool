// File: Interop/Structs/MONITORINFO.cs
using System.Runtime.InteropServices;

namespace ImmersiveWindow.Interop.Structs;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct Monitorinfo
{
    public int cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;
}