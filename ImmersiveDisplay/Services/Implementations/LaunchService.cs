using System.Diagnostics;

namespace ImmersiveDisplay.Services.Implementations;

public class LaunchService(ILoggingService loggingService) : ILaunchService
{
    private readonly HashSet<string> _launchedPaths = new(StringComparer.OrdinalIgnoreCase);

    public void Launch(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return;

        try
        {
            if (!_launchedPaths.Add(commandLine))
            {
                loggingService.AddLog($"> Associated program already launched in this session. Skipping: {commandLine}");
                return;
            }

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
            // Optional: Remove from the set if it truly failed so it can be retried, 
            // but failing here usually means a bad path, so keeping it locked is fine.
        }
    }
}
