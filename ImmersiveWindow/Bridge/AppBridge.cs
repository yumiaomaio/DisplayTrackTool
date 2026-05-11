using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ImmersiveWindow.Services;
using ImmersiveWindow.ViewModels;

namespace ImmersiveWindow.Bridge;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class AppBridge(MainViewModel viewModel, IProcessService processService)
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
        return processService.GetProcessIconBase64(processName);
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
