using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services;

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
    bool IsBackgroundOverlayEnabled();
    void SetEnableBackgroundOverlay(bool enabled);
    bool IsTaskbarAutoHideEnabled();
    void SetEnableTaskbarAutoHide(bool enabled);
}
