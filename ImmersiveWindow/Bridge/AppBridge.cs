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
            
            if (value is Enum) value = value.ToString()?.ToLower();

            // Ensure WebView2 is accessed on the UI thread
            System.Windows.Application.Current.Dispatcher.BeginInvoke(async () => {
                await PushToFrontend(new Dictionary<string, object?> { { e.PropertyName, value } });
            });
        };

        // --- Automatic Sync: Log Collection ---
        viewModel.Logs.CollectionChanged += (s, e) =>
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(async () => {
                await PushToFrontend(new { Logs = viewModel.Logs });
            });
        };
    }

    private async Task PushToFrontend(object state)
    {
        if (_webView == null) return;
        try
        {
            var options = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase 
            };
            string json = JsonSerializer.Serialize(state, options);
            await _webView.ExecuteScriptAsync($"window.onStateChanged({json})");
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
    public string AssociatedLaunchPath => viewModel.AssociatedLaunchPath ?? "";
    public bool LaunchOnAppStartup => viewModel.LaunchOnAppStartup;
    public bool LaunchOnTaskStart => viewModel.LaunchOnTaskStart;

    // --- Methods from JS ---
    public void StartMonitoring(string processName)
    {
        viewModel.TargetProcessName = processName;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => viewModel.StartCommand.Execute(null)));
    }

    public void StopMonitoring()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => viewModel.StopCommand.Execute(null)));
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

    public void SetAssociatedLaunchPath(string path)
    {
        viewModel.AssociatedLaunchPath = path;
    }

    public void SetLaunchOnAppStartup(bool enable)
    {
        viewModel.LaunchOnAppStartup = enable;
    }

    public void SetLaunchOnTaskStart(bool enable)
    {
        viewModel.LaunchOnTaskStart = enable;
    }

    public void SelectAssociatedProgram()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => viewModel.SelectAssociatedProgramCommand.Execute(null)));
    }

    public void SelectImage()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => viewModel.SelectImageCommand.Execute(null)));
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

    public bool CheckProcessExists(string processName)
    {
        return processService.GetProcessExecutablePath(processName) != null;
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
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => viewModel.AboutCommand.Execute(null)));
    }
    
    // Helper to get logs as a single string (can be optimized later)
    public string[] GetLogs()
    {
        return viewModel.Logs.ToArray();
    }
}
