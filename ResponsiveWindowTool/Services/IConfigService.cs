// File: Services/IConfigService.cs
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services
{
    public interface IConfigService
    {
        string GetDefaultProcessName();
        LayoutProfile GetPortraitProfile();
        LayoutProfile GetLandscapeProfile();
    }
}