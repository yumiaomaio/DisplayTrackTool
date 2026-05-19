// File: Services/ITargetStateManager.cs
namespace ImmersiveDisplay.Services;

public interface ITargetStateManager
{
    event Action<bool> IsRunningChanged;
    event Action<int>? WaitingCountdownChanged;
    bool IsRunning { get; }
    int WaitingCountdown { get; }
    Task StartAsync(string processName);
    Task StopAsync();
}