namespace ImmersiveDisplay.Models;

public record InitialState
{
    public required string TargetProcessName { get; init; }
    public required bool IsRunning { get; init; }
    public required bool IsAdmin { get; init; }
    public required bool EnableTaskbarAutoHide { get; init; }
    public required bool EnableDisplaySync { get; init; }
    public required bool EnableBackgroundOverlay { get; init; }
    public required string BackgroundMode { get; init; }
    public required string? CurrentImageFileName { get; init; }
    public required string BackgroundColor { get; init; }
    public required bool ShouldShowExitTip { get; init; }
    public required string? AssociatedLaunchPath { get; init; }
    public required bool LaunchOnAppStartup { get; init; }
    public required bool LaunchOnTaskStart { get; init; }
    public required bool AutoStartFromThirdParty { get; init; }
    public required bool AutoStartMonitoringOnProtocolLaunch { get; init; }
    public required bool ShouldShowUacPrompt { get; init; }
    public required bool IsProtocolRegistered { get; init; }
    public required int WaitingCountdown { get; init; }
    public required int WindowDetectionTimeout { get; init; }
    public required string[] Logs { get; init; }
}
