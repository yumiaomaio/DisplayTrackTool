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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref Devmode lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref Devmode lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorinfoEx lpmi);

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(QueryDisplayConfigFlags flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(QueryDisplayConfigFlags flags, ref uint numPathArrayElements, [In, Out] DisplayconfigPathInfo[] pathArray, ref uint numModeInfoArrayElements, [In, Out] DisplayconfigModeInfo[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(uint numPathArrayElements, [In] DisplayconfigPathInfo[] pathArray, uint numModeInfoArrayElements, [In] DisplayconfigModeInfo[] modeInfoArray, SetDisplayConfigFlags flags);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref DisplayconfigSourceDeviceName requestPacket);
}
