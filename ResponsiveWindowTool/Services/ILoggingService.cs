using System.Collections.ObjectModel;

namespace ResponsiveWindowTool.Services
{
    public interface ILoggingService
    {
        ObservableCollection<string> Logs { get; }
        void AddLog(string message);
    }
}