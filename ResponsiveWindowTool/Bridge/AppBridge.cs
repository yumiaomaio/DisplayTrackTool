using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ResponsiveWindowTool.ViewModels;

namespace ResponsiveWindowTool.Bridge;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class AppBridge(MainViewModel viewModel)
{
    // --- Properties to JS ---
    public string TargetProcessName => viewModel.TargetProcessName ?? "";
    public string PortraitAspectRatio => viewModel.PortraitAspectRatio ?? "";
    public bool IsRunning => viewModel.IsRunning;
    public bool IsAdmin => viewModel.IsAdmin;
    public bool EnableTaskbarAutoHide => viewModel.EnableTaskbarAutoHide;
    public bool EnableBackgroundOverlay => viewModel.EnableBackgroundOverlay;
    public string CurrentImageFileName => viewModel.CurrentImageFileName ?? "";
    public string BackgroundColor => viewModel.BackgroundColor;

    // --- Methods from JS ---
    public void StartMonitoring(string processName)
    {
        viewModel.TargetProcessName = processName;
        viewModel.StartCommand.Execute(null);
    }

    public void StopMonitoring()
    {
        viewModel.StopCommand.Execute(null);
    }

    public void SetPortraitAspectRatio(string ratio)
    {
        viewModel.PortraitAspectRatio = ratio;
    }

    public void SetBackgroundColor(string color)
    {
        viewModel.BackgroundColor = color;
    }

    public void SetEnableTaskbarAutoHide(bool enable)
    {
        viewModel.EnableTaskbarAutoHide = enable;
    }

    public void SetEnableBackgroundOverlay(bool enable)
    {
        viewModel.EnableBackgroundOverlay = enable;
    }

    public void SelectImage()
    {
        viewModel.SelectImageCommand.Execute(null);
    }

    public void ClearImage()
    {
        viewModel.ClearImageCommand.Execute(null);
    }

    public string GetImageBase64(string fileName)
    {
        try
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
            string fullPath = Path.Combine(backgroundsDir, fileName);
            if (!File.Exists(fullPath)) return "";

            byte[] imageBytes = File.ReadAllBytes(fullPath);
            string base64String = Convert.ToBase64String(imageBytes);
            string extension = Path.GetExtension(fullPath).ToLower().TrimStart('.');
            // Simple mime type detection
            string mimeType = extension == "png" ? "image/png" : "image/jpeg";
            return $"data:{mimeType};base64,{base64String}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppBridge] Error encoding image to base64: {ex.Message}");
            return "";
        }
    }

    public string GetProcessIconBase64(string processName)
    {
        try
        {
            if (string.IsNullOrEmpty(processName)) return "";
            
            // Remove .exe if present for searching
            string searchName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                ? processName.Substring(0, processName.Length - 4) 
                : processName;

            var processes = Process.GetProcessesByName(searchName);
            var process = processes.FirstOrDefault();
            
            if (process == null) return "";

            string? filePath;
            try 
            { 
                filePath = process.MainModule?.FileName; 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppBridge] Error accessing process module: {ex.Message}");
                return "";
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "";

            using (Icon? icon = Icon.ExtractAssociatedIcon(filePath))
            {
                if (icon == null) return "";
                using (Bitmap bitmap = icon.ToBitmap())
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    byte[] iconBytes = ms.ToArray();
                    return "data:image/png;base64," + Convert.ToBase64String(iconBytes);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppBridge] Error extracting process icon: {ex.Message}");
            return "";
        }
    }

    public void RestartAsAdmin()
    {
        viewModel.RestartAsAdminCommand.Execute(null);
    }

    public void ExitApp()
    {
        viewModel.ExitCommand.Execute(null);
    }

    public void ShowAbout()
    {
        viewModel.AboutCommand.Execute(null);
    }
    
    // Helper to get logs as a single string (can be optimized later)
    public string[] GetLogs()
    {
        return viewModel.Logs.ToArray();
    }
}