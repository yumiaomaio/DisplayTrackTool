// File: Helpers/ProtocolHelper.cs

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;
using ImmersiveDisplay.Models;

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

    public static void CreateMultipleUrlShortcuts(List<UrlEntryDto> entries, string? iconFileName = null)
    {
        // Ensure protocol is registered first
        if (!IsRegistered())
            Register();

        string iconPath;
        if (!string.IsNullOrEmpty(iconFileName))
        {
            iconPath = Path.Combine(AppContext.BaseDirectory, "icons", iconFileName);
            if (!File.Exists(iconPath))
                iconPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        }
        else
        {
            iconPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        }
        string iconLine = string.IsNullOrEmpty(iconPath) ? "" : $"\r\nIconIndex=0\r\nIconFile={iconPath}";

        foreach (var entry in entries)
        {
            string sanitizedName = string.Join("_", entry.Name.Split(Path.GetInvalidFileNameChars()));
            string content = $"[InternetShortcut]\r\nURL={ProtocolScheme}{AutoStartArg}{iconLine}";

            if (entry.Locations.StartMenu)
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"{sanitizedName}.url"), content);
            }
            if (entry.Locations.Desktop)
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                File.WriteAllText(Path.Combine(dir, $"{sanitizedName}.url"), content);
            }
        }
    }

    public static bool CleanAllAssociationUrls()
    {
        // Clear registry key
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProtocolName}", false);
        }
        catch { /* key may not exist */ }

        string[] dirs =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        bool deletedAny = false;
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.url"))
            {
                try
                {
                    string? urlLine = File.ReadLines(file)
                        .FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                    if (urlLine != null && urlLine.Trim().StartsWith($"URL={ProtocolScheme}", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(file);
                        deletedAny = true;
                    }
                }
                catch { /* skip locked files */ }
            }
        }
        return deletedAny;
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
