using System.Collections.ObjectModel;

namespace ImmersiveDisplay.Services;

public interface ILoggingService
{
    ObservableCollection<string> Logs { get; }
    void AddLog(string message);
    void AddLogs(params ReadOnlySpan<string> messages);
    void EnableFileLogging(bool enable);
}