// File: Services/IDisplaySettingService.cs
using ResponsiveWindowTool.Interop.Structs;

namespace ResponsiveWindowTool.Services
{
    public interface IDisplaySettingService
    {
        bool ApplySettings(string deviceName, int width, int height, uint dpi, LUID adapterId, uint sourceId);
    }
}