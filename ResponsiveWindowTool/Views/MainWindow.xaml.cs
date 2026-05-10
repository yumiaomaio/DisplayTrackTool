using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using ResponsiveWindowTool.Bridge;
using ResponsiveWindowTool.ViewModels;

namespace ResponsiveWindowTool.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Task<CoreWebView2Environment> _envTask;
    private AppBridge? _bridge;

    public MainWindow(MainViewModel viewModel, Task<CoreWebView2Environment> envTask)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _envTask = envTask;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;

        // Subscribe to state changes to push to JS
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Logs.CollectionChanged += Logs_CollectionChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Use the pre-created environment task
            var env = await _envTask;
            await WebView.EnsureCoreWebView2Async(env);

            // Disable F12 Developer Tools and Context Menus
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            _bridge = new AppBridge(_viewModel);
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

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_bridge == null || WebView.CoreWebView2 == null) return;

        // Push specific changes to JS
        if (e.PropertyName == nameof(_viewModel.IsRunning))
        {
            await UpdateJsState(new { IsRunning = _viewModel.IsRunning });
        }
        else if (e.PropertyName == nameof(_viewModel.CurrentImageFileName))
        {
            await UpdateJsState(new { CurrentImageFileName = _viewModel.CurrentImageFileName });
        }
    }

    private async void Logs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        await UpdateJsState(new { Logs = _viewModel.Logs });
    }

    private async Task UpdateJsState(object state)
    {
        if (WebView.CoreWebView2 == null) return;
        string json = JsonSerializer.Serialize(state);
        await WebView.CoreWebView2.ExecuteScriptAsync($"window.onStateChanged('{json}')");
    }
}