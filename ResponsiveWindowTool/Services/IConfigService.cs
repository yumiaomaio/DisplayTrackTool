// File: Services/IConfigService.cs
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services
{
    public interface IConfigService
    {
        string GetDefaultProcessName();
        LayoutProfile GetPortraitProfile();
        LayoutProfile GetLandscapeProfile();
        string? GetBackgroundImageFileName(); // <-- 新增方法
        void SetBackgroundImageFileName(string? fileName); // <-- 新增方法
    }
}