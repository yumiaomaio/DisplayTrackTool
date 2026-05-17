using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace ImmersiveDisplay.Services.Implementations;

public class PrivilegeService(ILoggingService loggingService) : IPrivilegeService
{
    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void RestartAsAdministrator()
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
            Application.Current.Shutdown();
        }
        catch (Win32Exception ex)
        {
            // User cancelled the UAC prompt
            loggingService.AddLog($"[PrivilegeService] User cancelled UAC elevation: {ex.Message}");
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[PrivilegeService] Error during elevation: {ex.Message}");
            MessageBox.Show($"Failed to restart as administrator: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}