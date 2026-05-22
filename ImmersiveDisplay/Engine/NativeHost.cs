using System.Runtime.InteropServices;

namespace ImmersiveDisplay;

internal static class NativeHost
{
    [DllImport("host.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern unsafe void Host_Start(
        delegate* unmanaged<IntPtr, void> onMessage,
        delegate* unmanaged<int, int, void> onResized,
        delegate* unmanaged<IntPtr, void> onReady);

    [DllImport("host.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern void Host_PostMessage(IntPtr ctx, IntPtr jsonUtf8);

    [DllImport("host.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern void Host_Shutdown(IntPtr ctx);
}
