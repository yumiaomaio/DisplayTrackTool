// File: Services/Implementations/PrivilegeService.cs

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace ImmersiveDisplay.Services.Implementations;

public class PrivilegeService(ILoggingService loggingService, IDialogService dialogService) : IPrivilegeService
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
            Environment.Exit(0);
        }
        catch (Win32Exception ex)
        {
            // User cancelled the UAC prompt
            loggingService.AddLog($"[PrivilegeService] User cancelled UAC elevation: {ex.Message}");
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[PrivilegeService] Error during elevation: {ex.Message}");
            dialogService.ShowError($"Failed to restart as administrator: {ex.Message}", "Error");
        }
    }
}