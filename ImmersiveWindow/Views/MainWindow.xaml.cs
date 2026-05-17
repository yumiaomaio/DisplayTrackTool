using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using ImmersiveWindow.Bridge;
using ImmersiveWindow.Services;
using ImmersiveWindow.ViewModels;

namespace ImmersiveWindow.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IProcessService _processService;
    private readonly Task<CoreWebView2Environment> _envTask;
    private AppBridge? _bridge;

    public MainWindow(MainViewModel viewModel, IProcessService processService, Task<CoreWebView2Environment> envTask)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _processService = processService;
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

            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            _bridge = new AppBridge(_viewModel, _processService);
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
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 Initialization failed: {ex.Message}");
        }
    }
}
