using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct ProcessBasicInformation
{
    public IntPtr ExitStatus;
    public IntPtr PebBaseAddress;
    public IntPtr AffinityMask;
    public IntPtr BasePriority;
    public UIntPtr UniqueProcessId;
    public IntPtr InheritedFromUniqueProcessId;
}
