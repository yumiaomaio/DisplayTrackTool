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

            var (fileName, arguments, workingDir) = ParseCommandLine(commandLine);
            
            if (IsProcessRunning(fileName))
            {
                loggingService.AddLog($"> Program is already running in the system. Skipping launch: {fileName}");
                return;
            }

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

    private bool IsProcessRunning(string exePath)
    {
        try
        {
            // 排除网址类协议，比如 steam://
            if (exePath.Contains("://") || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return false;

            string processName = Path.GetFileNameWithoutExtension(exePath);
            
            // 查找同名进程
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return false;

            foreach (var p in processes)
            {
                try
                {
                    if (string.Equals(p.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // 某些系统进程或高权限进程无法读取 MainModule，直接忽略
                }
            }
            
            // 如果有同名进程运行，这里默认返回 true (即使用户权限不够读不到完整路径)
            return true; 
        }
        catch (Exception)
        {
            return false;
        }
    }
}
