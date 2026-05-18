using System.Collections.ObjectModel;

namespace ImmersiveDisplay.Services;

public interface ILoggingService
{
    ObservableCollection<string> Logs { get; }
    void AddLog(string message);
    void EnableFileLogging(bool enable);
}