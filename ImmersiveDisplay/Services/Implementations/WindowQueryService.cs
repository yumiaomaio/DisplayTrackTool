// File: Services/Implementations/WindowQueryService.cs

using System.Diagnostics;

namespace ImmersiveDisplay.Services.Implementations;

public class WindowQueryService(ILoggingService loggingService) : IWindowQueryService
{
    public IntPtr? FindWindowByProcessName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var process = processes.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        
        if (process == null)
        {
            loggingService.AddLog($"[WindowQueryService] Process '{processName}' not found or has no main window.");
            return null;
        }
        
        loggingService.AddLog($"[WindowQueryService] Found process '{processName}' with MainWindowHandle: {process.MainWindowHandle}");
        return process.MainWindowHandle;
    }
}