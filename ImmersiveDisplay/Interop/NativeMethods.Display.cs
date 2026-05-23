using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Interop;

internal static partial class NativeMethods
{
    public const int ENUM_CURRENT_SETTINGS = -1;
    public const int CDS_UPDATEREGISTRY = 0x01;
    public const int DISP_CHANGE_SUCCESSFUL = 0;

    public const uint DM_PELSWIDTH = 0x00080000;
    public const uint DM_PELSHEIGHT = 0x00100000;
    public const uint DM_DISPLAYORIENTATION = 0x00800000;

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref Devmode lpDevMode);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorinfoEx lpmi);

    [LibraryImport("user32.dll")]
    public static partial int GetDisplayConfigBufferSizes(QueryDisplayConfigFlags flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [LibraryImport("user32.dll")]
    public static partial int QueryDisplayConfig(QueryDisplayConfigFlags flags, ref uint numPathArrayElements, [In, Out] DisplayconfigPathInfo[] pathArray, ref uint numModeInfoArrayElements, [In, Out] DisplayconfigModeInfo[] modeInfoArray, IntPtr currentTopologyId);

    [LibraryImport("user32.dll")]
    public static partial int SetDisplayConfig(uint numPathArrayElements, [In] DisplayconfigPathInfo[] pathArray, uint numModeInfoArrayElements, [In] DisplayconfigModeInfo[] modeInfoArray, SetDisplayConfigFlags flags);

    [LibraryImport("user32.dll")]
    public static partial int DisplayConfigGetDeviceInfo(ref DisplayconfigSourceDeviceName requestPacket);

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOptions dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr hMonitor, ref Monitorinfo lpmi);
}
