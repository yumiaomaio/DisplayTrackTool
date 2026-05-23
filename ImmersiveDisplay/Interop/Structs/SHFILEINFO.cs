using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Explicit, Size = 696)]
public struct Shfileinfo
{
    [FieldOffset(0)]
    public IntPtr hIcon;
    [FieldOffset(8)]
    public int iIcon;
    [FieldOffset(12)]
    public uint dwAttributes;
}
