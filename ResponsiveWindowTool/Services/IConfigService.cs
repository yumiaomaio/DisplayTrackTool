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
        BackgroundMode GetBackgroundMode();
        string GetBackgroundColor();
        void SetBackgroundMode(BackgroundMode mode);
        void SetBackgroundColor(string color);
        ResolutionConfig GetTargetResolution();
        bool IsConfirmationRequired();
        void SetTargetResolution(int width, int height, int dpi); // <-- 新增
        void SetRequireConfirmation(bool required); // <-- 新增
    }
}
