using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ImmersiveDisplay.Services.Implementations;

public class LoggingService : ILoggingService
{
    public ObservableCollection<string> Logs { get; } = new();

    private string? LogFilePath { get; set; }

    private bool _fileLoggingEnabled;
    private readonly Lock _fileLock = new();
    private readonly Lock _logLock = new();

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
            LogFilePath = Path.Combine(logsDir, fileName);

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

        // Thread-safe collection update (ObservableCollection is not thread-safe)
        lock (_logLock)
        {
            Logs.Insert(0, logEntry);
            while (Logs.Count > 100)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        }

        // Debug Output
        Debug.WriteLine(logEntry);

        // File Output
        if (_fileLoggingEnabled && LogFilePath != null)
        {
            WriteToFile(logEntry);
        }
    }

    public void AddLogs(params ReadOnlySpan<string> messages)
    {
        foreach (var msg in messages)
        {
            AddLog(msg);
        }
    }

    private void WriteToFile(string text)
    {
        if (LogFilePath == null) return;
        
        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(LogFilePath, text + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoggingService] Failed to write to log file: {ex.Message}");
            }
        }
    }
}