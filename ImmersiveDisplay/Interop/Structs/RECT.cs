// File: Interop/Structs/RECT.cs

using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct Rect
{
    public int Left, Top, Right, Bottom;
}