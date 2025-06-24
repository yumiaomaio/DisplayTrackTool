// File: Services/Implementations/DisplaySettingService.cs (Modified for Stage 2)
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class DisplaySettingService : IDisplaySettingService
    {
        /// <summary>
        /// Applies the specified resolution and DPI settings to a display device.
        /// </summary>
        /// <param name="deviceName">The GDI device name (e.g., \\.\DISPLAY1), used for logging.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="dpi">The target DPI percentage (e.g., 100, 125, 150).</param>
        /// <param name="adapterId">The LUID of the display adapter.</param>
        /// <param name="sourceId">The source ID of the display.</param>
        /// <returns>True if all settings were applied successfully; otherwise, false.</returns>
        public bool ApplySettings(string deviceName, int width, int height, uint dpi, LUID adapterId, uint sourceId)
        {
            Debug.WriteLine($"[DisplaySettingService] Applying settings: {width}x{height} @ {dpi}% DPI to device '{deviceName}' using CCD API.");

            // Step 1: Apply the new resolution using the modern CCD API.
            bool resolutionSuccess = ApplyResolutionWithCCD(width, height, adapterId, sourceId);
            if (!resolutionSuccess)
            {
                Debug.WriteLine("[DisplaySettingService] Failed to apply resolution using SetDisplayConfig. Aborting.");
                return false;
            }

            // A short delay can sometimes help the system process the resolution change before the DPI change.
            System.Threading.Thread.Sleep(100);

            // Step 2: Apply the new DPI. This part of the logic was already using modern APIs and remains unchanged.
            bool dpiSuccess = ApplyDpi(dpi, adapterId, sourceId);
            if (!dpiSuccess)
            {
                Debug.WriteLine("[DisplaySettingService] Resolution was changed, but failed to apply DPI. The operation is considered unsuccessful.");
                // In a more complex scenario, we might attempt to roll back the resolution change here.
                return false;
            }
            
            Debug.WriteLine("[DisplaySettingService] All settings (Resolution and DPI) applied successfully.");
            return true;
        }

        /// <summary>
        /// Changes the screen resolution for a specific display using the SetDisplayConfig API.
        /// </summary>
        private bool ApplyResolutionWithCCD(int width, int height, LUID adapterId, uint sourceId)
        {
            // 1. Get the required buffer sizes for the display configuration.
            int result = NativeMethods.GetDisplayConfigBufferSizes(
                QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, 
                out uint pathCount, 
                out uint modeCount);

            if (result != 0)
            {
                Debug.WriteLine($"[DisplaySettingService] GetDisplayConfigBufferSizes failed with error code: {result}");
                return false;
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            // 2. Query the current active display configuration.
            result = NativeMethods.QueryDisplayConfig(
                QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount, paths,
                ref modeCount, modes,
                IntPtr.Zero);

            if (result != 0)
            {
                Debug.WriteLine($"[DisplaySettingService] QueryDisplayConfig failed with error code: {result}");
                return false;
            }

            // 3. Find the specific display path that matches our target window's monitor.
            //    This is crucial for multi-monitor setups.
            int targetModeIndex = -1;
            for (int i = 0; i < pathCount; i++)
            {
                // Compare adapter LUID and source ID to find the correct path.
                // A direct struct comparison is not reliable, so compare fields.
                if (paths[i].sourceInfo.adapterId.LowPart == adapterId.LowPart && 
                    paths[i].sourceInfo.adapterId.HighPart == adapterId.HighPart && 
                    paths[i].sourceInfo.id == sourceId)
                {
                    targetModeIndex = (int)paths[i].sourceInfo.modeInfoIdx;
                    Debug.WriteLine($"[DisplaySettingService] Found matching display path at index {i} with modeInfo index {targetModeIndex}.");
                    break;
                }
            }

            if (targetModeIndex == -1)
            {
                Debug.WriteLine("[DisplaySettingService] Could not find a matching display path for the given adapter/source ID. Cannot change resolution.");
                return false;
            }

            // 4. Modify the source mode of the found path with the new resolution.
            ref var sourceMode = ref modes[targetModeIndex].modeInfo.sourceMode;
            Debug.WriteLine($"[DisplaySettingService] Current resolution is {sourceMode.width}x{sourceMode.height}. Changing to {width}x{height}.");
            sourceMode.width = (uint)width;
            sourceMode.height = (uint)height;
            
            // 5. Apply the modified configuration to the system.
            var flags = SDCFlags.SDC_APPLY | SDCFlags.SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDCFlags.SDC_ALLOW_CHANGES;
            result = NativeMethods.SetDisplayConfig(pathCount, paths, modeCount, modes, flags);

            if (result != 0)
            {
                Debug.WriteLine($"[DisplaySettingService] SetDisplayConfig failed with error code: {result}");
                return false;
            }

            Debug.WriteLine("[DisplaySettingService] SetDisplayConfig applied resolution successfully.");
            return true;
        }

        /// <summary>
        /// Applies a specific DPI scaling percentage to a display. This logic remains unchanged.
        /// </summary>
        private bool ApplyDpi(uint dpiPercent, LUID adapterId, uint sourceId)
        {
            // This logic is borrowed from other open-source projects and works by calculating a relative scale value.
            uint[] dpiVals = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };
            int targetDpiIndex = Array.IndexOf(dpiVals, dpiPercent);
            
            if (targetDpiIndex < 0)
            {
                Debug.WriteLine($"[DisplaySettingService] DPI value {dpiPercent}% is not a standard supported value.");
                return false;
            }

            var getDpiRequest = new DISPLAYCONFIG_GET_DPI
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_DPI>(),
                    adapterId = adapterId,
                    id = sourceId
                }
            };
            
            if (NativeMethods.DisplayConfigGetDeviceInfo(ref getDpiRequest) != 0)
            {
                Debug.WriteLine("[DisplaySettingService] Failed to get current DPI info before setting.");
                return false;
            }
            
            // Calculate the relative scale value required by the API.
            int recommendedOffset = Math.Abs(getDpiRequest.minScaleRel);
            int relativeScale = targetDpiIndex - recommendedOffset;

            var setDpiRequest = new DISPLAYCONFIG_SET_DPI
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DisplayConfigDeviceInfoType.DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_DPI>(),
                    adapterId = adapterId,
                    id = sourceId
                },
                scaleRel = relativeScale
            };

            int result = NativeMethods.DisplayConfigSetDeviceInfo(ref setDpiRequest);
            bool success = result == 0;
            if (!success)
            {
                 Debug.WriteLine($"[DisplaySettingService] DisplayConfigSetDeviceInfo for DPI failed with error code: {result}");
            }
            return success;
        }
    }
}