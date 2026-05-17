using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services;

public interface IConfigService
{
    string GetDefaultProcessName();
    void SetDefaultProcessName(string processName);
    LayoutProfile GetPortraitProfile();
    LayoutProfile GetLandscapeProfile();
    string? GetBackgroundImageFileName();
    void SetBackgroundImageFileName(string? fileName);
    BackgroundMode GetBackgroundMode();
    string GetBackgroundColor();
    void SetBackgroundMode(BackgroundMode mode);
    void SetBackgroundColor(string color);
    bool IsBackgroundOverlayEnabled();
    void SetEnableBackgroundOverlay(bool enabled);
    bool IsTaskbarAutoHideEnabled();
    void SetEnableTaskbarAutoHide(bool enabled);
    bool IsDisplaySyncEnabled();
    void SetEnableDisplaySync(bool enabled);
    bool ShouldShowExitTip();
    void SetShowExitTip(bool show);
    string? GetAssociatedLaunchPath();
    void SetAssociatedLaunchPath(string? path);
    bool IsLaunchOnAppStartupEnabled();
    void SetLaunchOnAppStartup(bool enabled);
    bool IsLaunchOnTaskStartEnabled();
    void SetLaunchOnTaskStart(bool enabled);
    bool IsAutoStartFromThirdPartyEnabled();
    void SetAutoStartFromThirdParty(bool enabled);
}
