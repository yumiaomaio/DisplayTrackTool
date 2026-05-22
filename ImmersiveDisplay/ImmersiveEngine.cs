using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

namespace ImmersiveDisplay;

/// <summary>
/// The main engine for the Immersive Display application.
/// Sets up DI, creates the OverlayHost thread, and orchestrates service initialization.
/// </summary>
public class ImmersiveEngine : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private bool _isInitialized;

    /// <summary>
    /// Gets the OverlayHost managing the overlay thread (HWND operations, hooks).
    /// Available after Initialize().
    /// </summary>
    public OverlayHost? OverlayHost { get; private set; }

    /// <summary>
    /// Optional callback for forwarding Bridge state pushes to the host.
    /// Set this BEFORE calling Initialize() so state pushes during init are captured.
    /// </summary>
    public Action<string>? OnStatePush { get; set; }

    /// <summary>
    /// Gets the AppBridge for frontend communication.
    /// Available after Initialize().
    /// </summary>
    public AppBridge? Bridge { get; private set; }

    public bool IsProtocolAutoStart { get; set; }

    /// <summary>
    /// Initializes DI, the overlay thread, and all services.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        // 1. Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 2. Start OverlayHost (dedicated thread with message pump)
        OverlayHost = _serviceProvider.GetRequiredService<OverlayHost>();
        OverlayHost.Start();
        OverlayHost.InstallKeyboardHook();

        // 3. Setup Bridge and wire up state forwarding BEFORE any service logic runs
        Bridge = _serviceProvider.GetRequiredService<AppBridge>();
        if (OnStatePush != null)
            Bridge.OnMessageSent += OnStatePush;
        Bridge.Initialize();

        // 4. Initialize hooks and startup logic
        var loggingService = _serviceProvider.GetRequiredService<ILoggingService>();
        var appIntegrationService = _serviceProvider.GetRequiredService<IAppIntegrationService>();
        appIntegrationService.IsProtocolAutoStart = IsProtocolAutoStart;
        appIntegrationService.InitializeHooksAndTriggers();
        appIntegrationService.ExecuteStartupLogic();

        _isInitialized = true;
        loggingService.AddLog("[Engine] Immersive Engine initialized successfully.");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ITaskbarService, TaskbarService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IPrivilegeService, PrivilegeService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IWindowQueryService, WindowQueryService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
        services.AddSingleton<ITargetStateManager, TargetStateManager>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILaunchService, LaunchService>();
        services.AddSingleton<IProtocolService, ProtocolService>();
        services.AddSingleton<IDialogService, NativeDialogService>();
        services.AddSingleton<IOverlayImageService, OverlayImageService>();
        services.AddSingleton<IAppIntegrationService, AppIntegrationService>();

        // OverlayHost (DI injects IConfigService + ILoggingService)
        services.AddSingleton<OverlayHost>();

        // Logic Bridge
        services.AddSingleton<AppBridge>();
    }

    public void Dispose()
    {
        if (!_isInitialized) return;

        // Unsubscribe state forwarding
        if (Bridge != null && OnStatePush != null)
            Bridge.OnMessageSent -= OnStatePush;

        Bridge?.Dispose();
        Bridge = null;

        // Stop overlay thread (cleans up hooks + overlay window)
        OverlayHost?.StopWindowMonitoring();
        OverlayHost?.UninstallKeyboardHook();

        var overlayService = _serviceProvider?.GetService<IOverlayService>();
        overlayService?.Hide();

        OverlayHost?.Stop();
        OverlayHost?.Dispose();
        OverlayHost = null;

        _serviceProvider?.Dispose();
        _isInitialized = false;
    }
}
