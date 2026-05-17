using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using ImmersiveWindow.Bridge;
using ImmersiveWindow.Services;
using ImmersiveWindow.ViewModels;
using ImmersiveWindow.Interop;

namespace ImmersiveWindow.Views;

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
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

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

            // --- Associated Launch (App Startup) ---
            if (_configService.IsLaunchOnAppStartupEnabled())
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
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string path = files[0];
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
    }
}
