// File: Helpers/DpiHelper.cs

using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Helpers;

public static partial class DpiHelper
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(IntPtr value);

    [LibraryImport("shcore.dll", SetLastError = true)]
    private static partial int SetProcessDpiAwareness(int value);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAware();

    /// <summary>
    /// Configures the process to be DPI-aware. 
    /// Tries the most modern API (Per-Monitor V2) first, falling back to older APIs as needed.
    /// </summary>
    public static void ConfigureDpiAwareness()
    {
        try
        {
            // Try SetProcessDpiAwarenessContext (Windows 10 1703+)
            // -4 corresponds to DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
            {
                // Fallback 1: SetProcessDpiAwareness (Windows 8.1+)
                // 2 corresponds to PROCESS_PER_MONITOR_DPI_AWARE
                SetProcessDpiAwareness(2);
            }
        }
        catch
        {
            try
            {
                // Fallback 2: SetProcessDPIAware (Windows Vista+)
                SetProcessDpiAware();
            }
            catch
            {
                // Ignore fallback exceptions
            }
        }
    }
}
