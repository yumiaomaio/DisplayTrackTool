using System.Collections.ObjectModel;

namespace ImmersiveWindow.Services;

public interface ILoggingService
{
    ObservableCollection<string> Logs { get; }
    void AddLog(string message);
}