using System.IO;
using System.Windows;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Services;
using Microsoft.Web.WebView2.Core;

namespace ImmersiveDisplay.Views;

public partial class MainWindow : Window
{
    private readonly AppBridge _bridge;
    private readonly IAppIntegrationService _appIntegrationService;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private readonly Task<CoreWebView2Environment> _envTask;

    public MainWindow(
        AppBridge bridge, 
        IAppIntegrationService appIntegrationService, 
        IConfigService configService,
        ILoggingService loggingService,
        Task<CoreWebView2Environment> envTask)
    {
        InitializeComponent();
        _bridge = bridge;
        _appIntegrationService = appIntegrationService;
        _configService = configService;
        _loggingService = loggingService;
        _envTask = envTask;

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await _envTask;
            await WebView.EnsureCoreWebView2Async(env);

            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // --- Drag and Drop via Navigation Interception ---
            WebView.CoreWebView2.NavigationStarting += (s, ev) =>
            {
                if (!ev.Uri.Contains("/WebUI/", StringComparison.OrdinalIgnoreCase))
                {
                    ev.Cancel = true;
                    HandleExternalNavigation(ev.Uri);
                }
            };

            WebView.CoreWebView2.NewWindowRequested += (s, ev) =>
            {
                if (!ev.Uri.Contains("/WebUI/", StringComparison.OrdinalIgnoreCase))
                {
                    ev.Handled = true;
                    HandleExternalNavigation(ev.Uri);
                }
            };

            _bridge.Initialize(WebView.CoreWebView2);
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

            // Initialize app integrations & launch startup scripts
            _appIntegrationService.InitializeHooksAndTriggers();
            _appIntegrationService.ExecuteStartupLogic();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 Initialization failed: {ex.Message}");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _bridge.Dispose();
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
        string targetPath;

        if (extension == ".lnk")
        {
            targetPath = ShortcutResolver.Resolve(path);
        }
        else if (extension == ".exe")
        {
            targetPath = path.Contains(' ') ? $"\"{path}\"" : path;
        }
        else
        {
            targetPath = path;
        }

        _configService.SetAssociatedLaunchPath(targetPath);
    }
}
