// File: Services/ITargetStateManager.cs
using System;
using System.Collections.ObjectModel;

using System.Threading.Tasks;

namespace ResponsiveWindowTool.Services
{
    public interface ITargetStateManager
    {
        event Action<bool> IsRunningChanged;
        Task StartAsync(string processName);
        Task StopAsync();
    }
}