using System.Diagnostics;
using Microsoft.Win32;

namespace ImmersiveDisplay.Services.Implementations;

public class ProtocolService(IConfigService configService, ILoggingService loggingService) : IProtocolService
{
    private const string ProtocolName = "immersivedisplay";
    private const string ProtocolScheme = "immersivedisplay://";
    private const string AutoStartArg = "autostart";
    private const string ShortcutName = "Immersive Auto Launch.url";

    public bool Register()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Could not determine executable path.");
            
            // 1. Register URL Protocol in Registry
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}"))
            {
                key.SetValue("", $"URL:{ProtocolName} Protocol");
                key.SetValue("URL Protocol", "");
                
                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    shellKey.SetValue("", $"\"{exePath}\" \"%1\"");
                }

                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue("", $"{exePath},0");
                }
            }

            // 2. Create .url shortcuts
            CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exePath);
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), exePath);

            loggingService.AddLog($"[ProtocolService] Protocol and shortcuts registered to: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProtocolService] ERROR during registration: {ex.Message}");
            return false;
        }
    }

    public bool Unregister()
    {
        try
        {
            // 1. Remove Registry Key
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProtocolName}", false);

            // 2. Delete Shortcuts
            DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"));

            loggingService.AddLog("[ProtocolService] Protocol and shortcuts unregistered.");
            return true;
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProtocolService] ERROR during unregistration: {ex.Message}");
            return false;
        }
    }

    public void UpdateIfNecessary()
    {
        if (!configService.IsAutoStartFromThirdPartyEnabled()) return;

        try
        {
            if (!IsAssociationValid())
            {
                loggingService.AddLog("[ProtocolService] Association invalid or Start Menu shortcut missing. Restoring associations...");
                Register();
            }
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProtocolService] ERROR during automatic association validation: {ex.Message}");
        }
    }

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}");
        return key != null;
    }

    public bool IsAssociationValid()
    {
        try
        {
            // 1. Check registry open command exists
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}\shell\open\command");
            if (key == null) return false;

            // 2. Check if registered path matches current EXE path
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath)) return false;

            string? registeredCommand = key.GetValue("") as string;
            if (string.IsNullOrEmpty(registeredCommand) || !registeredCommand.Contains(currentExePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 3. Check if Start Menu shortcut exists (desktop shortcut is excluded as users often delete them)
            string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ShortcutName);
            if (!File.Exists(startMenuPath))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CreateShortcut(string folderPath, string exePath)
    {
        if (!Directory.Exists(folderPath)) return;
        
        string shortcutPath = Path.Combine(folderPath, ShortcutName);
        string content = $"[InternetShortcut]\r\nURL={ProtocolScheme}{AutoStartArg}\r\nIconIndex=0\r\nIconFile={exePath}";
        
        File.WriteAllText(shortcutPath, content);
    }

    private void DeleteShortcut(string folderPath)
    {
        string shortcutPath = Path.Combine(folderPath, ShortcutName);
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}
