// File: Services/Implementations/DisplaySettingService.cs
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
        public bool ApplySettings(string deviceName, int width, int height, uint dpi, LUID adapterId, uint sourceId)
        {
            Debug.WriteLine($"[DisplaySettingService] Applying settings: {width}x{height} @ {dpi}% DPI to device '{deviceName}'.");

            bool resolutionSuccess = ApplyResolution(deviceName, width, height);
            if (!resolutionSuccess)
            {
                Debug.WriteLine("[DisplaySettingService] Failed to apply resolution. Aborting.");
                return false;
            }

            // 在应用DPI之前稍作等待，有时系统需要一点时间来处理分辨率的变更
            System.Threading.Thread.Sleep(100);

            bool dpiSuccess = ApplyDpi(dpi, adapterId, sourceId);
            if (!dpiSuccess)
            {
                Debug.WriteLine("[DisplaySettingService] Failed to apply DPI. Resolution was changed, but DPI was not.");
                // 在这种情况下，我们可能选择返回true或false，取决于业务需求。
                // 返回false更安全，因为它表示整个操作未完全成功。
                return false;
            }
            
            Debug.WriteLine("[DisplaySettingService] All settings applied successfully.");
            return true;
        }

        private bool ApplyResolution(string deviceName, int width, int height)
        {
            var devMode = new DEVMODE
            {
                dmSize = (ushort)Marshal.SizeOf<DEVMODE>(),
                dmDeviceName = deviceName,
                dmFields = DeviceModeFields.DM_PELSWIDTH | DeviceModeFields.DM_PELSHEIGHT,
                dmPelsWidth = (uint)width,
                dmPelsHeight = (uint)height
            };

            // CDS_UPDATEREGISTRY: 将设置写入注册表
            // CDS_NORESET: 立即应用更改，不提示重启
            int result = NativeMethods.ChangeDisplaySettingsEx(
                deviceName,
                ref devMode,
                IntPtr.Zero,
                ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_NORESET,
                IntPtr.Zero);

            bool success = result == 0; // DISP_CHANGE_SUCCESSFUL
            if (!success)
            {
                Debug.WriteLine($"[DisplaySettingService] ChangeDisplaySettingsEx failed with code: {result}");
            }
            return success;
        }

        private bool ApplyDpi(uint dpiPercent, LUID adapterId, uint sourceId)
        {
            // 这部分逻辑从 BorderlessWindowApp 的 DisplayScaleService 中借鉴和简化
            uint[] dpiVals = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

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
            
            // 计算相对缩放值
            int recommendedOffset = Math.Abs(getDpiRequest.minScaleRel);
            int recommendedDpiIndex = recommendedOffset;
            int targetDpiIndex = Array.IndexOf(dpiVals, dpiPercent);
            
            if (targetDpiIndex < 0)
            {
                Debug.WriteLine($"[DisplaySettingService] DPI value {dpiPercent}% is not a standard value.");
                return false; // 不支持非标准的DPI值
            }

            int relativeScale = targetDpiIndex - recommendedDpiIndex;

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
                 Debug.WriteLine($"[DisplaySettingService] DisplayConfigSetDeviceInfo failed with code: {result}");
            }
            return success;
        }
    }
}