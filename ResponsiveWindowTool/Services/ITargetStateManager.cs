// File: Services/ITargetStateManager.cs
namespace ResponsiveWindowTool.Services;

public interface ITargetStateManager
{
    event Action<bool> IsRunningChanged;
    Task StartAsync(string processName);
    Task StopAsync();
}