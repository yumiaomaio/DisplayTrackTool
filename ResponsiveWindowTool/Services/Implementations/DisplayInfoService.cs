// File: Services/Implementations/DisplayInfoService.cs (FINAL REFACTORED VERSION)
using System;
using System.Diagnostics;
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
            // --- Stage 1: Get the GDI device name of the target monitor ---

            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                Debug.WriteLine("[DisplayInfoService] Failed to get monitor from window handle.");
                return null;
            }

            var monitorInfoEx = new MONITORINFOEX();
            monitorInfoEx.cbSize = Marshal.SizeOf(monitorInfoEx);
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfoEx))
            {
                Debug.WriteLine("[DisplayInfoService] GetMonitorInfoEx failed.");
                return null;
            }

            string targetDeviceName = monitorInfoEx.szDevice;
            Debug.WriteLine($"[DisplayInfoService] Target window is on monitor with GDI name: '{targetDeviceName}'");

            // --- Stage 2: Query all active display paths ---

            if (NativeMethods.GetDisplayConfigBufferSizes(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            {
                Debug.WriteLine("[DisplayInfoService] GetDisplayConfigBufferSizes failed.");
                return null;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (NativeMethods.QueryDisplayConfig(QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                Debug.WriteLine("[DisplayInfoService] QueryDisplayConfig failed.");
                return null;
            }

            // --- Stage 3: Iterate through paths and find the one matching our GDI device name ---

            foreach (var path in paths)
            {
                // For each path, get its source information (adapter and source ID)
                var sourceInfo = path.sourceInfo;

                // Prepare a request to get the GDI device name for this specific source
                var sourceNameRequest = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                sourceNameRequest.header.type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceNameRequest.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                sourceNameRequest.header.adapterId = sourceInfo.adapterId;
                sourceNameRequest.header.id = sourceInfo.id;

                // Call the API to fill the request structure with the device name
                if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceNameRequest) == 0)
                {
                    Debug.WriteLine($"[DisplayInfoService] Checking path... CCD source (Adapter: {sourceInfo.adapterId.LowPart}, ID: {sourceInfo.id}) -> GDI Name: '{sourceNameRequest.viewGdiDeviceName}'");

                    // Direct string comparison. This is robust and DPI-unaware.
                    if (targetDeviceName.Equals(sourceNameRequest.viewGdiDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Match found!
                        Debug.WriteLine($"[DisplayInfoService] SUCCESS: Found matching path. Identifiers are ready.");
                        return new DisplayIdentifiers
                        {
                            DeviceName = targetDeviceName, // or sourceNameRequest.viewGdiDeviceName
                            AdapterId = sourceInfo.adapterId,
                            SourceId = sourceInfo.id
                        };
                    }
                }
            }

            Debug.WriteLine($"[DisplayInfoService] CRITICAL: Could not find a display path matching GDI device name '{targetDeviceName}'.");
            return null;
        }

        // The GetCurrentState method remains unchanged as it relies on the now-robust GetIdentifiers.
        public DisplaySnapshot? GetCurrentState(IntPtr hwnd)
        {
            var identifiers = GetIdentifiers(hwnd);
            if (identifiers?.DeviceName == null) return null;

            var devMode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (!NativeMethods.EnumDisplaySettingsEx(identifiers.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            {
                return null;
            }

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

            uint currentDpi = 100;
            if (NativeMethods.DisplayConfigGetDeviceInfo(ref dpiRequest) == 0)
            {
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