// File: Helpers/ProtocolHelper.cs

using System.Diagnostics;
using Microsoft.Win32;

namespace ImmersiveDisplay.Helpers;

public static class ProtocolHelper
{
    private const string ProtocolName = "immersivedisplay";
    private const string ProtocolScheme = "immersivedisplay://";
    private const string AutoStartArg = "autostart";
    private const string ShortcutName = "Immersive Auto Launch.url";

    public static bool Register()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Could not determine executable path.");
            
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

            CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exePath);
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), exePath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProtocolName}", false);

            DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"));

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}");
        return key != null;
    }

    public static bool IsAssociationValid()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}\shell\open\command");
            if (key == null) return false;

            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExePath)) return false;

            string? registeredCommand = key.GetValue("") as string;
            if (string.IsNullOrEmpty(registeredCommand) || !registeredCommand.Contains(currentExePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ShortcutName);
            return File.Exists(startMenuPath);
        }
        catch
        {
            return false;
        }
    }

    private static void CreateShortcut(string folderPath, string exePath)
    {
        if (!Directory.Exists(folderPath)) return;
        
        string shortcutPath = Path.Combine(folderPath, ShortcutName);
        string content = $"[InternetShortcut]\r\nURL={ProtocolScheme}{AutoStartArg}\r\nIconIndex=0\r\nIconFile={exePath}";
        
        File.WriteAllText(shortcutPath, content);
    }

    private static void DeleteShortcut(string folderPath)
    {
        string shortcutPath = Path.Combine(folderPath, ShortcutName);
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}
