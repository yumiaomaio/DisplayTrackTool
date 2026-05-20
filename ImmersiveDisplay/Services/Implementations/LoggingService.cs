using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace ImmersiveDisplay.Services.Implementations;

public class LoggingService : ILoggingService
{
    public ObservableCollection<string> Logs { get; } = new();
    
    private string? _logFilePath;
    private bool _fileLoggingEnabled;
    private readonly object _fileLock = new();

    public void EnableFileLogging(bool enable)
    {
        _fileLoggingEnabled = enable;
        if (!_fileLoggingEnabled) return;

        try
        {
            string logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            string fileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            _logFilePath = Path.Combine(logsDir, fileName);
            
            // Initial marker
            WriteToFile($"--- Log Started: {DateTime.Now} ---");
        }
        catch (Exception ex)
        {
            _fileLoggingEnabled = false;
            Debug.WriteLine($"[LoggingService] Failed to initialize file logging: {ex.Message}");
        }
    }

    public void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] {message}";

        // UI Update
        ImmersiveDisplay.Helpers.UiDispatcher.BeginInvoke(() =>
        {
            Logs.Insert(0, logEntry);
            while (Logs.Count > 100)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        });

        // Debug Output
        Debug.WriteLine(logEntry);

        // File Output
        if (_fileLoggingEnabled && _logFilePath != null)
        {
            WriteToFile(logEntry);
        }
    }

    private void WriteToFile(string text)
    {
        if (_logFilePath == null) return;
        
        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(_logFilePath, text + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoggingService] Failed to write to log file: {ex.Message}");
            }
        }
    }
}