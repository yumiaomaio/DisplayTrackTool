// File: Services/Implementations/DisplayInfoService.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class DisplayInfoService : IDisplayInfoService
    {
        public DisplayIdentifiers? GetIdentifiers(IntPtr hwnd)
        {
            // 1. Get the monitor handle from the window handle.
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                Debug.WriteLine("[DisplayInfoService] Failed to get monitor from window handle.");
                return null;
            }

            // 2. Get the monitor's screen coordinates. This is our primary key for matching.
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                Debug.WriteLine("[DisplayInfoService] Failed to get monitor info.");
                return null;
            }
            var monitorRect = monitorInfo.rcMonitor;

            // 3. Use QueryDisplayConfig to get modern display path information.
            if (NativeMethods.GetDisplayConfigBufferSizes(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            {
                return null;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (NativeMethods.QueryDisplayConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                return null;
            }

            // 4. Find the correct display path by matching the monitor's position.
            foreach (var mode in modes)
            {
                if ((DISPLAYCONFIG_MODE_INFO_TYPE)mode.infoType != DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    continue;
                }
                if (mode.sourceMode.position.x != monitorRect.Left || mode.sourceMode.position.y != monitorRect.Top)
                {
                    continue;
                }
                
                // Found the matching source mode. Now find its GDI device name.
                // This is done by enumerating GDI devices and matching their position.
                var displayDevice = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                for (uint i = 0; NativeMethods.EnumDisplayDevices(null, i, ref displayDevice, 0); i++)
                {
                    if ((displayDevice.StateFlags & 1 /* DISPLAY_DEVICE_ACTIVE */) == 0) continue;

                    var devMode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
                    if (NativeMethods.EnumDisplaySettingsEx(displayDevice.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode, 0))
                    {
                        if (devMode.dmPositionX == monitorRect.Left && devMode.dmPositionY == monitorRect.Top)
                        {
                            // Success! We found all identifiers.
                            Debug.WriteLine($"[DisplayInfoService] Found match: Device={displayDevice.DeviceName}, AdapterID={mode.adapterId.LowPart}, SourceID={mode.id}");
                            return new DisplayIdentifiers
                            {
                                DeviceName = displayDevice.DeviceName,
                                AdapterId = mode.adapterId,
                                SourceId = mode.id
                            };
                        }
                    }
                    displayDevice.cb = Marshal.SizeOf<DISPLAY_DEVICE>(); // Reset for next iteration
                }
            }

            Debug.WriteLine($"[DisplayInfoService] Could not find a matching display for monitor at {monitorRect.Left},{monitorRect.Top}.");
            return null;
        }

        public DisplaySnapshot? GetCurrentState(IntPtr hwnd)
        {
            var identifiers = GetIdentifiers(hwnd);
            if (identifiers?.DeviceName == null) return null;

            // Get Resolution
            var devMode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (!NativeMethods.EnumDisplaySettingsEx(identifiers.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            {
                return null;
            }

            // Get DPI
            var dpiRequest = new DISPLAYCONFIG_GET_DPI
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_DPI>(),
                    adapterId = identifiers.AdapterId,
                    id = identifiers.SourceId
                }
            };

            uint currentDpi = 100; // Default DPI
            if (NativeMethods.DisplayConfigGetDeviceInfo(ref dpiRequest) == 0)
            {
                // This logic is borrowed from BorderlessWindowApp. It converts a relative scale value to a percentage.
                uint[] dpiVals = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };
                int offset = Math.Abs(dpiRequest.minScaleRel);
                if (dpiVals.Length > offset + dpiRequest.curScaleRel)
                {
                    currentDpi = dpiVals[offset + dpiRequest.curScaleRel];
                }
            }

            return new DisplaySnapshot
            {
                DeviceName = identifiers.DeviceName,
                Width = (int)devMode.dmPelsWidth,
                Height = (int)devMode.dmPelsHeight,
                Dpi = currentDpi
            };
        }
    }
}