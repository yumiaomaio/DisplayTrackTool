namespace ImmersiveDisplay.Services.Implementations;

/// <summary>
/// Thin facade that delegates overlay operations to the OverlayHost (overlay thread).
/// Keeps IOverlayService interface stable for existing consumers.
/// </summary>
public class OverlayService : IOverlayService
{
    private readonly OverlayHost _overlayHost;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private IntPtr _lastTargetHwnd = IntPtr.Zero;

    public IntPtr? WindowHandle => _overlayHost.OverlayHwnd != IntPtr.Zero ? _overlayHost.OverlayHwnd : null;

    public OverlayService(OverlayHost overlayHost, IConfigService configService, ILoggingService loggingService)
    {
        _overlayHost = overlayHost;
        _configService = configService;
        _loggingService = loggingService;

        _configService.ConfigChanged += OnConfigChanged;
    }

    private void OnConfigChanged(string key, object? value)
    {
        if (_lastTargetHwnd == IntPtr.Zero) return;

        switch (key)
        {
            case "EnableBackgroundOverlay":
                bool enabled = value is bool b && b;
                _loggingService.AddLog($"[OverlayService] Toggle Overlay: {enabled}");
                if (enabled) Show(_lastTargetHwnd);
                else Hide();
                break;

            case "BackgroundMode":
            case "BackgroundColor":
            case "CurrentImageFileName":
                _loggingService.AddLog($"[OverlayService] Config '{key}' changed. Refreshing overlay...");
                Show(_lastTargetHwnd);
                break;
        }
    }

    public void Show(IntPtr targetHwnd)
    {
        _lastTargetHwnd = targetHwnd;
        _overlayHost.ShowOverlay(targetHwnd);
    }

    public void Hide()
    {
        _lastTargetHwnd = IntPtr.Zero;
        _overlayHost.HideOverlay();
    }

    public void UpdatePosition(IntPtr targetHwnd)
    {
        if (_overlayHost.OverlayHwnd == IntPtr.Zero) return;
        _loggingService.AddLog("[OverlayService] Orientation change detected. Re-syncing overlay window...");
        _overlayHost.ShowOverlay(targetHwnd);
    }
}
