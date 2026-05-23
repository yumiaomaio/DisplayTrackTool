// File: Helpers/PrivilegeHelper.cs

using System.Diagnostics;
using System.Security.Principal;

namespace ImmersiveDisplay.Helpers;

public static class PrivilegeHelper
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdministrator()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = Process.GetCurrentProcess().MainModule?.FileName,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            Process.Start(processInfo);
            Environment.Exit(0);
        }
        catch
        {
            // Caller handles cancellation/errors if needed, or we just fail silently here
        }
    }
}
