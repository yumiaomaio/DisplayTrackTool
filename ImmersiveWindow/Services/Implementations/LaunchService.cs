using System.Diagnostics;

namespace ImmersiveWindow.Services.Implementations;

public class LaunchService(ILoggingService loggingService) : ILaunchService
{
    public void Launch(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return;

        try
        {
            loggingService.AddLog($"> Launching associated program: {commandLine}");
            
            // Use cmd /c start to handle both URLs and paths with arguments robustly
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" {commandLine}",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"> ERROR: Failed to launch associated program: {ex.Message}");
        }
    }
}
