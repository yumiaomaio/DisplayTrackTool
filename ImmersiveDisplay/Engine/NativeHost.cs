using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Engine;

internal static partial class NativeHost
{
    [LibraryImport("host.dll")]
    public static unsafe partial void Host_Start(
        delegate* unmanaged<IntPtr, void> onMessage,
        delegate* unmanaged<IntPtr, IntPtr, void> onReady);

    [LibraryImport("host.dll")]
    public static partial void Host_PostMessage(IntPtr ctx, IntPtr jsonUtf8);

    [LibraryImport("host.dll")]
    public static partial void Host_Shutdown(IntPtr ctx);
}
