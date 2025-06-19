using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services
{
    public interface IConfigService
    {
        string GetDefaultProcessName();
        void SetDefaultProcessName(string processName); // 新增
        LayoutProfile GetPortraitProfile();
        LayoutProfile GetLandscapeProfile();
        string? GetBackgroundImageFileName();
        void SetBackgroundImageFileName(string? fileName);
        double GetPortraitAspectRatio(); // 新增
        void SetPortraitAspectRatio(double aspectRatio); // 新增
    }
}
