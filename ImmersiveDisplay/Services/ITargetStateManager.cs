// File: Services/ITargetStateManager.cs
namespace ImmersiveDisplay.Services;

public interface ITargetStateManager
{
    event Action<bool> IsRunningChanged;
    event Action<int>? WaitingCountdownChanged;
    bool IsRunning { get; }
    int WaitingCountdown { get; }
    IntPtr? CurrentTargetHwnd { get; }
    Task StartAsync(string processName, bool programAlreadyLaunched = false);
    Task StopAsync();
}