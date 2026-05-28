using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services.Implementations;

public class DisplayService(ILoggingService loggingService) : IDisplayService
 {
    private DisplayProfile? _originalDisplayProfile;
    private string? _capturedDeviceName;

    public void CaptureOriginalState(IntPtr hwnd)
    {
        try
        {
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MonitorinfoEx { cbSize = Marshal.SizeOf<MonitorinfoEx>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                loggingService.AddLog($"[DisplayService] Failed to capture monitor info for HWND {hwnd}.");
                return;
            }

            string deviceName = monitorInfo.szDevice.ToString();
            _capturedDeviceName = deviceName;
            
            var devMode = new Devmode { dmSize = (short)Marshal.SizeOf<Devmode>() };
            if (!NativeMethods.EnumDisplaySettings(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                loggingService.AddLog($"[DisplayService] Failed to enum settings for {deviceName}.");
                return;
            }

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
            loggingService.AddLog($"[DisplayService] Error capturing original profile: {ex.Message}");
        }
    }

    public void ApplyDisplayProfile(IntPtr hwnd, DisplayProfile? profile)
    {
        if (profile == null) return;

        // Prefer the captured device name if we have one, as it's more stable
        var targetDeviceName = _capturedDeviceName;
        if (string.IsNullOrEmpty(targetDeviceName))
        {
            targetDeviceName = GetGdiDeviceName(hwnd);
        }

        if (string.IsNullOrEmpty(targetDeviceName))
        {
            loggingService.AddLog($"[DisplayService] Failed to get GDI device name for HWND {hwnd}. (IsWindow: {NativeMethods.IsWindow(hwnd)})");
            return;
        }

        ApplyDisplayProfileInternal(targetDeviceName, profile);
    }

    private void ApplyDisplayProfileInternal(string targetDeviceName, DisplayProfile? profile)
    {
        if (profile == null || string.IsNullOrEmpty(targetDeviceName)) return;

        try
        {
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
                    
                    // Apply change
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
        
        string? targetDeviceName = _capturedDeviceName;
        if (string.IsNullOrEmpty(targetDeviceName) && hwnd != IntPtr.Zero)
        {
            targetDeviceName = GetGdiDeviceName(hwnd);
        }

        if (!string.IsNullOrEmpty(targetDeviceName))
        {
            ApplyDisplayProfileInternal(targetDeviceName, _originalDisplayProfile);
        }
        else
        {
            loggingService.AddLog("[DisplayService] Cannot restore display: device name is empty.");
        }
        
        _originalDisplayProfile = null;
        _capturedDeviceName = null;
    }

    public DisplayConfigRotation? GetCurrentDisplayRotation(IntPtr hwnd)
    {
        try
        {
            var deviceName = _capturedDeviceName;
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = GetGdiDeviceName(hwnd);
            }
            if (string.IsNullOrEmpty(deviceName))
            {
                return null;
            }

            if (NativeMethods.GetDisplayConfigBufferSizes(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            {
                return null;
            }

            var paths = new DisplayconfigPathInfo[pathCount];
            var modes = new DisplayconfigModeInfo[modeCount];

            if (NativeMethods.QueryDisplayConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                return null;
            }

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
                    if (deviceName.Equals(sourceNameRequest.viewGdiDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return paths[i].targetInfo.rotation;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[DisplayService] GetCurrentDisplayRotation error: {ex.Message}");
        }

        return null;
    }

    private string GetGdiDeviceName(IntPtr hwnd)
    {
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            loggingService.AddLog($"[DisplayService] MonitorFromWindow returned NULL for HWND {hwnd}.");
            return string.Empty;
        }

        var monitorInfo = new MonitorinfoEx { cbSize = Marshal.SizeOf<MonitorinfoEx>() };
        if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            return monitorInfo.szDevice.ToString();
        }

        loggingService.AddLog($"[DisplayService] GetMonitorInfo failed. hMonitor: {hMonitor}, cbSize: {monitorInfo.cbSize}");
        return string.Empty;
    }

    internal static DisplayConfigRotation MapToCcdRotation(int orientation)
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
