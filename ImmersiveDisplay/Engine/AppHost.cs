using System.Runtime.CompilerServices;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Services;
using Microsoft.Extensions.DependencyInjection;

[assembly: DisableRuntimeMarshalling]

namespace ImmersiveDisplay.Engine;

public class AppHost : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private bool _isInitialized;

    /// <summary>
    /// Optional callback for forwarding Bridge state pushes to the host.
    /// Set this BEFORE calling Initialize() so state pushes during init are captured.
    /// </summary>
    public Action<string>? OnStatePush { get; set; }

    public AppBridge? Bridge { get; private set; }

    public bool IsProtocolAutoStart { get; set; }

    public void Initialize()
    {
        if (_isInitialized) return;

        // 1. Setup DI
        var services = new ServiceCollection();
        services.AddImmersiveServices();
        _serviceProvider = services.BuildServiceProvider();

        // 2. Start message pump thread
        var windowThread = _serviceProvider.GetRequiredService<WindowThread>();
        windowThread.Start();

        // 3. Install keyboard hook (runs on WindowThread)
        var keyboardHook = _serviceProvider.GetRequiredService<IKeyboardHookService>();
        keyboardHook.Install();

        // 4. Setup Bridge and wire up state forwarding BEFORE any service logic runs
        Bridge = _serviceProvider.GetRequiredService<AppBridge>();
        if (OnStatePush != null)
            Bridge.OnMessageSent += OnStatePush;
        Bridge.Initialize();

        // 5. Initialize hooks and startup logic
        var loggingService = _serviceProvider.GetRequiredService<ILoggingService>();
        var appIntegrationService = _serviceProvider.GetRequiredService<IAppIntegrationService>();
        appIntegrationService.IsProtocolAutoStart = IsProtocolAutoStart;
        appIntegrationService.InitializeHooksAndTriggers();
        appIntegrationService.ExecuteStartupLogic();

        _isInitialized = true;
        loggingService.AddLog("[Engine] Immersive Engine initialized successfully.");
    }

    public void Dispose()
    {
        if (!_isInitialized) return;

        // Unsubscribe state forwarding
        if (Bridge != null && OnStatePush != null)
            Bridge.OnMessageSent -= OnStatePush;

        Bridge?.Dispose();
        Bridge = null;

        // Shutdown services in reverse order
        _serviceProvider?.GetService<IWindowMonitorService>()?.StopMonitoring();
        _serviceProvider?.GetService<IKeyboardHookService>()?.Uninstall();
        _serviceProvider?.GetService<IOverlayService>()?.Hide();
        _serviceProvider?.GetService<WindowThread>()?.Stop();
        _serviceProvider?.GetService<WindowThread>()?.Dispose();

        _serviceProvider?.Dispose();
        _isInitialized = false;
    }
}
