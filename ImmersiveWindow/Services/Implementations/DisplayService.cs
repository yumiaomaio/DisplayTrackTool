using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveWindow.Interop;
using ImmersiveWindow.Interop.Enums;
using ImmersiveWindow.Interop.Structs;
using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services.Implementations;

public class DisplayService(ILoggingService loggingService) : IDisplayService
{
    private DisplayProfile? _originalDisplayProfile;

    public void CaptureOriginalState(IntPtr hwnd)
    {
        try
        {
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MonitorinfoEx { cbSize = Marshal.SizeOf<MonitorinfoEx>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;

            string deviceName = monitorInfo.szDevice;
            var devMode = new Devmode { dmSize = (short)Marshal.SizeOf<Devmode>() };
            if (!NativeMethods.EnumDisplaySettings(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                return;

            _originalDisplayProfile = new DisplayProfile
            {
                Width = (int)devMode.dmPelsWidth,
                Height = (int)devMode.dmPelsHeight,
                Orientation = (int)devMode.dmDisplayOrientation
            };
            
            loggingService.AddLog($"[DisplayService] Original display state captured for {deviceName} (Orientation: {_originalDisplayProfile.Orientation}).");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DisplayService] Error capturing original profile: {ex.Message}");
        }
    }

    public void ApplyDisplayProfile(IntPtr hwnd, DisplayProfile? profile)
    {
        if (profile == null) return;

        try
        {
            // 1. Map HWND to CCD Path
            var targetDeviceName = GetGdiDeviceName(hwnd);
            if (string.IsNullOrEmpty(targetDeviceName))
            {
                loggingService.AddLog("[DisplayService] Failed to get GDI device name for HWND.");
                return;
            }

            loggingService.AddLog($"[DisplayService] Target GDI: {targetDeviceName}");

            // 2. Query CCD Config
            if (NativeMethods.GetDisplayConfigBufferSizes(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            {
                loggingService.AddLog("[DisplayService] Failed to get CCD buffer sizes.");
                return;
            }

            var paths = new DisplayconfigPathInfo[pathCount];
            var modes = new DisplayconfigModeInfo[modeCount];

            if (NativeMethods.QueryDisplayConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                loggingService.AddLog("[DisplayService] Failed to query CCD config.");
                return;
            }

            // 3. Find matching path
            int targetPathIdx = -1;
            for (int i = 0; i < pathCount; i++)
            {
                var sourceNameRequest = new DisplayconfigSourceDeviceName
                {
                    header = new DisplayconfigDeviceInfo_Header
                    {
                        type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        size = (uint)Marshal.SizeOf<DisplayconfigSourceDeviceName>(),
                        adapterId = paths[i].sourceInfo.adapterId,
                        id = paths[i].sourceInfo.id
                    }
                };

                if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceNameRequest) == 0)
                {
                    if (targetDeviceName.Equals(sourceNameRequest.viewGdiDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPathIdx = i;
                        break;
                    }
                }
            }

            if (targetPathIdx == -1)
            {
                loggingService.AddLog("[DisplayService] Could not find CCD path for GDI device.");
                return;
            }

            loggingService.AddLog($"[DisplayService] Found CCD Path index: {targetPathIdx}");

            // 4. Apply Rotation
            if (profile.Orientation.HasValue)
            {
                var newRotation = MapToCcdRotation(profile.Orientation.Value);
                if (paths[targetPathIdx].targetInfo.rotation != newRotation)
                {
                    loggingService.AddLog($"[DisplayService] Changing CCD rotation: {paths[targetPathIdx].targetInfo.rotation} -> {newRotation}");
                    paths[targetPathIdx].targetInfo.rotation = newRotation;
                    
                    // Apply change - Using the actual counts returned from QueryDisplayConfig
                    int result = NativeMethods.SetDisplayConfig(pathCount, paths, modeCount, modes, 
                        SetDisplayConfigFlags.SDC_APPLY | SetDisplayConfigFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG | SetDisplayConfigFlags.SDC_ALLOW_CHANGES);
                    
                    if (result == 0)
                    {
                        loggingService.AddLog("[DisplayService] CCD display settings applied successfully.");
                    }
                    else
                    {
                        loggingService.AddLog($"[DisplayService] SetDisplayConfig failed with error: {result}");
                    }
                }
                else
                {
                    loggingService.AddLog("[DisplayService] No rotation change needed.");
                }
            }
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[DisplayService] CCD Error: {ex.Message}");
        }
    }

    public void RestoreOriginalState(IntPtr hwnd)
    {
        if (_originalDisplayProfile == null) return;
        
        loggingService.AddLog("[DisplayService] Restoring original display settings...");
        ApplyDisplayProfile(hwnd, _originalDisplayProfile);
        _originalDisplayProfile = null;
    }

    private string GetGdiDeviceName(IntPtr hwnd)
    {
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MonitorinfoEx { cbSize = Marshal.SizeOf<MonitorinfoEx>() };
        if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            return monitorInfo.szDevice;
        }
        return string.Empty;
    }

    private DisplayConfigRotation MapToCcdRotation(int orientation)
    {
        return orientation switch
        {
            0 => DisplayConfigRotation.DISPLAYCONFIG_ROTATION_IDENTITY,
            1 => DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE90,
            2 => DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE180,
            3 => DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE270,
            _ => DisplayConfigRotation.DISPLAYCONFIG_ROTATION_IDENTITY
        };
    }
}
