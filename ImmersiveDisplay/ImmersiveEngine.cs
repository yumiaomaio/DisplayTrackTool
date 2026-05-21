using System.Runtime.CompilerServices;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

[assembly: DisableRuntimeMarshalling]

namespace ImmersiveDisplay;

/// <summary>
/// The main entry point for the Immersive Display Library.
/// Pure C# interface, decoupled from WebView2 and COM.
/// </summary>
public class ImmersiveEngine : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private bool _isInitialized;

    /// <summary>
    /// Gets the AppBridge instance to interact with the frontend.
    /// Available after calling Initialize().
    /// </summary>
    public AppBridge? Bridge { get; private set; }

    public bool IsProtocolAutoStart { get; set; }

    /// <summary>
    /// Initializes the Immersive Display logic.
    /// MUST be called on the main UI STA thread to setup message pumps.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        // 1. Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 2. Initialize UI Dispatcher with the internal hidden window
        var loggingService = _serviceProvider.GetRequiredService<ILoggingService>();
        UiDispatcher.Initialize(loggingService);

        // 3. Setup Bridge
        Bridge = _serviceProvider.GetRequiredService<AppBridge>();
        Bridge.Initialize();

        // 4. Initialize Core Hooks and Startup Logic
        var appIntegrationService = _serviceProvider.GetRequiredService<IAppIntegrationService>();
        appIntegrationService.IsProtocolAutoStart = IsProtocolAutoStart;
        appIntegrationService.InitializeHooksAndTriggers();
        appIntegrationService.ExecuteStartupLogic();

        _isInitialized = true;
        loggingService.AddLog("[Engine] Immersive Engine initialized successfully.");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core STATEFUL services
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
        services.AddSingleton<IDialogService, NativeDialogService>();
        services.AddSingleton<IOverlayImageService, OverlayImageService>();
        services.AddSingleton<IAppIntegrationService, AppIntegrationService>();

        // Logic Bridge
        services.AddSingleton<AppBridge>();
    }

    public void Dispose()
    {
        if (!_isInitialized) return;

        Bridge?.Dispose();
        Bridge = null;

        var keyboardHook = _serviceProvider?.GetService<IKeyboardHookService>();
        keyboardHook?.Stop();

        var monitorService = _serviceProvider?.GetService<IWindowMonitorService>();
        monitorService?.StopMonitoring();

        var overlayService = _serviceProvider?.GetService<IOverlayService>();
        overlayService?.Hide();

        UiDispatcher.Shutdown();
        _serviceProvider?.Dispose();
        _isInitialized = false;
    }
}
