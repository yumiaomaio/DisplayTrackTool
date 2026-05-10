// File: Interop/Structs/RECT.cs
using System.Runtime.InteropServices;

namespace ResponsiveWindowTool.Interop.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left, Top, Right, Bottom;
}