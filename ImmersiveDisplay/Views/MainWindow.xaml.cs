using System.IO;
using System.Windows;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace ImmersiveDisplay.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IProcessService _processService;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private readonly Task<CoreWebView2Environment> _envTask;
    private AppBridge? _bridge;

    public MainWindow(
        MainViewModel viewModel, 
        IProcessService processService, 
        IConfigService configService,
        ILoggingService loggingService,
        Task<CoreWebView2Environment> envTask)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _processService = processService;
        _configService = configService;
        _loggingService = loggingService;
        _envTask = envTask;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await _envTask;
            await WebView.EnsureCoreWebView2Async(env);

            // Disable standard WebView2 drop to let WPF handle it
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // --- Drag and Drop via Navigation Interception ---
            // When a file or link is dropped into WebView2, it tries to navigate to it.
            // We intercept anything that isn't part of our local WebUI.
            WebView.CoreWebView2.NavigationStarting += (s, e) =>
            {
                if (!e.Uri.Contains("/WebUI/", StringComparison.OrdinalIgnoreCase))
                {
                    e.Cancel = true;
                    HandleExternalNavigation(e.Uri);
                }
            };

            WebView.CoreWebView2.NewWindowRequested += (s, e) =>
            {
                if (!e.Uri.Contains("/WebUI/", StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    HandleExternalNavigation(e.Uri);
                }
            };

            _bridge = new AppBridge(_viewModel, _processService, _loggingService);
            _bridge.Initialize(WebView.CoreWebView2); // Start auto-sync
            
            WebView.CoreWebView2.AddHostObjectToScript("bridge", _bridge);
            
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebUI", "index.html");
            if (File.Exists(htmlPath))
            {
                WebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
            else
            {
                MessageBox.Show($"Web UI file not found at: {htmlPath}");
            }

            // --- Auto Start From Third Party ---
            bool autoStartedByThirdParty = false;
            if (_configService.IsAutoStartFromThirdPartyEnabled())
            {
                string? parentProcessName = _processService.GetParentProcessName()?.ToLowerInvariant();
                _loggingService.AddLog($"[Startup] Parent process: {parentProcessName ?? "Unknown"}");

                // List of common shell/dev launchers to ignore (treat as normal startup)
                var ignoredParents = new[] { "explorer", "cmd", "powershell", "pwsh", "rider64", "devenv", "bash", "mintty" };
                
                if (!string.IsNullOrEmpty(parentProcessName) && !ignoredParents.Contains(parentProcessName))
                {
                    _loggingService.AddLog($"[Startup] Third-party launcher detected. Auto-launching and starting monitoring.");
                    autoStartedByThirdParty = true;
                    
                    // 1. Launch associated program
                    _viewModel.LaunchAssociatedProgram();
                    
                    // 2. Start monitoring task
                    _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => 
                    {
                        if (!_viewModel.IsRunning && !string.IsNullOrWhiteSpace(_viewModel.TargetProcessName))
                        {
                            _viewModel.StartCommand.Execute(null);
                        }
                    }));
                }
            }

            // --- Associated Launch (App Startup) ---
            if (!autoStartedByThirdParty && _configService.IsLaunchOnAppStartupEnabled())
            {
                _viewModel.LaunchAssociatedProgram();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 Initialization failed: {ex.Message}");
        }
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Link;
            e.Handled = true;
        }
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                HandleFileDrop(files[0]);
            }
        }
    }

    private void HandleExternalNavigation(string uri)
    {
        try
        {
            // Convert file:/// URIs back to C:\ local paths
            // Keep other URIs (http, etc.) as they are
            string target = uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(uri).LocalPath
                : uri;
            
            HandleFileDrop(target);
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[DragDrop] Error handling navigation: {ex.Message}");
        }
    }

    private void HandleFileDrop(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        
        string extension = Path.GetExtension(path).ToLower();

        if (extension == ".lnk")
        {
            _viewModel.AssociatedLaunchPath = ShortcutResolver.Resolve(path);
        }
        else if (extension == ".exe")
        {
            // Quote if contains spaces
            _viewModel.AssociatedLaunchPath = path.Contains(' ') ? $"\"{path}\"" : path;
        }
        else
        {
            // Generic files or URLs (if possible)
            _viewModel.AssociatedLaunchPath = path;
        }
    }
}
