using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services
{
    public interface IConfigService
    {
        string GetDefaultProcessName();
        void SetDefaultProcessName(string processName);
        LayoutProfile GetPortraitProfile();
        LayoutProfile GetLandscapeProfile();
        string? GetBackgroundImageFileName();
        void SetBackgroundImageFileName(string? fileName);
        string? GetPortraitAspectRatio();
        void SetPortraitAspectRatio(string? aspectRatio);
    }
}
