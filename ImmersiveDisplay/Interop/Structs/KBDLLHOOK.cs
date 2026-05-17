using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct Kbdllhookstruct
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}