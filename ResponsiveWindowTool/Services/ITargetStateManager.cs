// File: Services/ITargetStateManager.cs
using System;
using System.Collections.ObjectModel;

namespace ResponsiveWindowTool.Services
{
    public interface ITargetStateManager
    {
        ObservableCollection<string> Logs { get; }
        void Start(string processName);
        void Stop();
    }
}