namespace ImmersiveDisplay.Services;

public interface IProcessService
{
    string GetProcessIconBase64(string processName);
    string? GetProcessExecutablePath(string processName);
    string? GetProcessCommandLine(string processName);
}
