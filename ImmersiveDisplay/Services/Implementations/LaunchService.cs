using System.Diagnostics;
using System.IO;

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

            var (fileName, arguments, workingDir) = ParseCommandLine(commandLine);
            
            loggingService.AddLog($"> Launching associated program: {fileName}");
            if (!string.IsNullOrWhiteSpace(arguments))
                loggingService.AddLog($"> With arguments: {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDir ?? "",
                UseShellExecute = true,
                CreateNoWindow = false // Let the target app decide its window state
            };
            
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"> ERROR: Failed to launch associated program: {ex.Message}");
        }
    }

    public void ClearHistory()
    {
        _launchedPaths.Clear();
        loggingService.AddLog("> Associated program launch history cleared.");
    }

    private (string FileName, string Arguments, string? WorkingDirectory) ParseCommandLine(string input)
    {
        input = input.Trim();

        // 1. Handle Protocol/URL (e.g., steam://, http://)
        if (input.Contains("://"))
        {
            return (input, "", null);
        }

        string fileName;
        string arguments = "";

        // 2. Handle Quoted Path
        if (input.StartsWith("\""))
        {
            int nextQuote = input.IndexOf("\"", 1);
            if (nextQuote != -1)
            {
                fileName = input.Substring(1, nextQuote - 1);
                arguments = input.Substring(nextQuote + 1).Trim();
            }
            else
            {
                fileName = input.Trim('"');
            }
        }
        else
        {
            // 3. Unquoted Path - Try to separate EXE from arguments
            // If the whole thing exists, it's just a path
            if (File.Exists(input))
            {
                fileName = input;
            }
            else
            {
                // Otherwise, split at the first space
                int firstSpace = input.IndexOf(' ');
                if (firstSpace != -1)
                {
                    fileName = input.Substring(0, firstSpace);
                    arguments = input.Substring(firstSpace + 1).Trim();
                }
                else
                {
                    fileName = input;
                }
            }
        }

        // 4. Determine Working Directory
        string? workingDir = null;
        try
        {
            if (File.Exists(fileName))
            {
                workingDir = Path.GetDirectoryName(fileName);
            }
        }
        catch
        {
            // Ignore IO errors
        }

        return (fileName, arguments, workingDir);
    }
}
