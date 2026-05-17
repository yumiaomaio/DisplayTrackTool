// File: Services/ITargetStateManager.cs
namespace ImmersiveDisplay.Services;

public interface ITargetStateManager
{
    event Action<bool> IsRunningChanged;
    Task StartAsync(string processName);
    Task StopAsync();
}