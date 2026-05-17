using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using ImmersiveWindow.Services;
using ImmersiveWindow.ViewModels;

namespace ImmersiveWindow.Bridge;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class AppBridge(MainViewModel viewModel, IProcessService processService)
{
    private CoreWebView2? _webView;

    /// <summary>
    /// Binds the bridge to the WebView and starts automatic state synchronization.
    /// </summary>
    public void Initialize(CoreWebView2 webView)
    {
        _webView = webView;
        
        // --- Automatic Sync: Property Changes ---
        viewModel.PropertyChanged += async (s, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)) return;
            
            // Extract the value using reflection
            var prop = viewModel.GetType().GetProperty(e.PropertyName);
            if (prop == null) return;
            
            var value = prop.GetValue(viewModel);
            
            // Format specific values if needed
            if (value is Enum) value = value.ToString()?.ToLower();

            await PushToFrontend(new Dictionary<string, object?> { { e.PropertyName, value } });
        };

        // --- Automatic Sync: Log Collection ---
        viewModel.Logs.CollectionChanged += async (s, e) =>
        {
            await PushToFrontend(new { Logs = viewModel.Logs });
        };
    }

    private async Task PushToFrontend(object state)
    {
        if (_webView == null) return;
        try
        {
            string json = JsonSerializer.Serialize(state);
            await _webView.ExecuteScriptAsync($"window.onStateChanged('{json}')");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppBridge] State push failed: {ex.Message}");
        }
    }

    // --- Properties to JS ---
    public string TargetProcessName => viewModel.TargetProcessName ?? "";
    public bool IsRunning => viewModel.IsRunning;
    public bool IsAdmin => viewModel.IsAdmin;
    public bool EnableTaskbarAutoHide => viewModel.EnableTaskbarAutoHide;
    public bool EnableDisplaySync => viewModel.EnableDisplaySync;
    public bool EnableBackgroundOverlay => viewModel.EnableBackgroundOverlay;
    public string BackgroundMode => viewModel.BackgroundMode.ToString().ToLower();
    public string CurrentImageFileName => viewModel.CurrentImageFileName ?? "";
    public string BackgroundColor => viewModel.BackgroundColor;
    public bool ShouldShowExitTip => viewModel.ShouldShowExitTip;

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

    public void SetBackgroundColor(string color)
    {
        viewModel.BackgroundColor = color;
    }

    public void SetEnableTaskbarAutoHide(bool enable)
    {
        viewModel.EnableTaskbarAutoHide = enable;
    }

    public void SetEnableDisplaySync(bool enable)
    {
        viewModel.EnableDisplaySync = enable;
    }

    public void SetEnableBackgroundOverlay(bool enable)
    {
        viewModel.EnableBackgroundOverlay = enable;
    }

    public void SetShowExitTip(bool show)
    {
        viewModel.ShouldShowExitTip = show;
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
