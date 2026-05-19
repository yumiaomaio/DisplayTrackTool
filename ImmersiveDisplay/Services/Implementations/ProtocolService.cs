using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ImmersiveDisplay.Services.Implementations;

public class ProtocolService(IConfigService configService, ILoggingService loggingService) : IProtocolService
{
    private const string ProtocolName = "immersivedisplay";
    private const string ProtocolScheme = "immersivedisplay://";
    private const string AutoStartArg = "autostart";
    private const string ShortcutName = "Immersive Auto Launch.url";

    public void Register()
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
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProtocolService] ERROR during registration: {ex.Message}");
        }
    }

    public void Unregister()
    {
        try
        {
            // 1. Remove Registry Key
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProtocolName}", false);

            // 2. Delete Shortcuts
            DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"));

            loggingService.AddLog("[ProtocolService] Protocol and shortcuts unregistered.");
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProtocolService] ERROR during unregistration: {ex.Message}");
        }
    }

    public void UpdateIfNecessary()
    {
        if (!configService.IsAutoStartFromThirdPartyEnabled()) return;

        try
        {
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}\shell\open\command");
            if (key == null)
            {
                loggingService.AddLog("[ProtocolService] Feature enabled but registry missing. Re-registering...");
                Register();
                return;
            }

            string? registeredCommand = key.GetValue("") as string;
            if (string.IsNullOrEmpty(registeredCommand) || !registeredCommand.Contains(currentExePath, StringComparison.OrdinalIgnoreCase))
            {
                loggingService.AddLog("[ProtocolService] Path mismatch detected. Updating associations...");
                Register();
            }
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProtocolService] ERROR during path check: {ex.Message}");
        }
    }

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}");
        return key != null;
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
