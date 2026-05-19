// File: App.xaml.cs

using System.Windows;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Implementations;
using ImmersiveDisplay.ViewModels;
using ImmersiveDisplay.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace ImmersiveDisplay;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Task<CoreWebView2Environment> _webViewEnvTask;

    public static bool IsProtocolAutoStart { get; private set; }

    public App()
    {
        // Start pre-creating the WebView2 environment as early as possible (Warm-up)
        _webViewEnvTask = CoreWebView2Environment.CreateAsync();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register the environment task so MainWindow can await it
        services.AddSingleton(_webViewEnvTask);

        // Register services as Singleton since this is a stateful desktop tool
        services.AddSingleton<ITaskbarService, TaskbarService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IPrivilegeService, PrivilegeService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IWindowQueryService, WindowQueryService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
        services.AddSingleton<IWindowMonitorService, WindowMonitorService>();
        services.AddSingleton<ITargetStateManager, TargetStateManager>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILaunchService, LaunchService>();
        services.AddSingleton<IKeyboardHookService, KeyboardHookService>();
        services.AddSingleton<IProtocolService, ProtocolService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();

        // Register Views
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Check for protocol activation
        if (e.Args.Length > 0)
        {
            foreach (var arg in e.Args)
            {
                if (arg.StartsWith("immersivedisplay://autostart", StringComparison.OrdinalIgnoreCase))
                {
                    IsProtocolAutoStart = true;
                    break;
                }
            }
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}