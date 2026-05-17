using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace ImmersiveDisplay.Services.Implementations;

public class LoggingService : ILoggingService
{
    public ObservableCollection<string> Logs { get; } = new();

    public void AddLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(logEntry);
            Logs.Insert(0, logEntry);
            while (Logs.Count > 100)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        });
    }
}