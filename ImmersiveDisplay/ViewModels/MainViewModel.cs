// File: ViewModels/MainViewModel.cs

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Services;
using Microsoft.Win32;

namespace ImmersiveDisplay.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITargetStateManager _stateManager;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private readonly IPrivilegeService _privilegeService;
    private readonly ILaunchService _launchService;

    private string? _targetProcessName;
    public string? TargetProcessName
    {
        get => _targetProcessName;
        set
        {
            if (SetProperty(ref _targetProcessName, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _configService.SetDefaultProcessName(value);
                }
            }
        }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    private bool _isAdmin;
    public bool IsAdmin
    {
        get => _isAdmin;
        private set => SetProperty(ref _isAdmin, value);
    }

    private bool _enableTaskbarAutoHide;
    public bool EnableTaskbarAutoHide
    {
        get => _enableTaskbarAutoHide;
        set
        {
            if (SetProperty(ref _enableTaskbarAutoHide, value))
            {
                _configService.SetEnableTaskbarAutoHide(value);
            }
        }
    }

    private bool _enableDisplaySync;
    public bool EnableDisplaySync
    {
        get => _enableDisplaySync;
        set
        {
            if (SetProperty(ref _enableDisplaySync, value))
            {
                _configService.SetEnableDisplaySync(value);
            }
        }
    }

    private BackgroundMode _backgroundMode;
    public BackgroundMode BackgroundMode
    {
        get => _backgroundMode;
        set => SetProperty(ref _backgroundMode, value);
    }

    public ObservableCollection<string> Logs => _loggingService.Logs;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SelectImageCommand { get; }
    public ICommand ClearImageCommand { get; }
    public ICommand RestartAsAdminCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand ExitCommand { get; }

    private string? _currentImageFileName;
    public string? CurrentImageFileName
    {
        get => _currentImageFileName;
        set
        {
            if (SetProperty(ref _currentImageFileName, value))
            {
                (ClearImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _backgroundColor = "#FF000000";
    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (SetProperty(ref _backgroundColor, value))
            {
                _configService.SetBackgroundColor(value);
            }
        }
    }

    #region New Properties for UI Binding
    private bool _enableBackgroundOverlay;
    public bool EnableBackgroundOverlay
    {
        get => _enableBackgroundOverlay;
        set
        {
            if (SetProperty(ref _enableBackgroundOverlay, value))
            {
                _configService.SetEnableBackgroundOverlay(value);
            }
        }
    }

    private bool _shouldShowExitTip;
    public bool ShouldShowExitTip
    {
        get => _shouldShowExitTip;
        set
        {
            if (SetProperty(ref _shouldShowExitTip, value))
            {
                _configService.SetShowExitTip(value);
            }
        }
    }

    private string? _associatedLaunchPath;
    public string? AssociatedLaunchPath
    {
        get => _associatedLaunchPath;
        set
        {
            if (SetProperty(ref _associatedLaunchPath, value))
            {
                _configService.SetAssociatedLaunchPath(value);
            }
        }
    }

    private bool _launchOnAppStartup;
    public bool LaunchOnAppStartup
    {
        get => _launchOnAppStartup;
        set
        {
            if (SetProperty(ref _launchOnAppStartup, value))
            {
                _configService.SetLaunchOnAppStartup(value);
            }
        }
    }

    private bool _launchOnTaskStart;
    public bool LaunchOnTaskStart
    {
        get => _launchOnTaskStart;
        set
        {
            if (SetProperty(ref _launchOnTaskStart, value))
            {
                _configService.SetLaunchOnTaskStart(value);
            }
        }
    }

    private bool _autoStartFromThirdParty;
    public bool AutoStartFromThirdParty
    {
        get => _autoStartFromThirdParty;
        set
        {
            if (SetProperty(ref _autoStartFromThirdParty, value))
            {
                _configService.SetAutoStartFromThirdParty(value);
            }
        }
    }
    #endregion

    public MainViewModel(
        ITargetStateManager stateManager, 
        IConfigService configService, 
        ILoggingService loggingService,
        IPrivilegeService privilegeService,
        ILaunchService launchService,
        IKeyboardHookService keyboardHookService)
    {
        _stateManager = stateManager;
        _configService = configService;
        _loggingService = loggingService;
        _privilegeService = privilegeService;
        _launchService = launchService;

        // Start global keyboard hook for F9 (Start) and F12 (Stop)
        keyboardHookService.Start();
        keyboardHookService.KeyPressed += (vkCode) => 
        {
            const int vkF9 = 0x78;
            const int vkF12 = 0x7B;

            if (vkCode == vkF9)
            {
                if (!IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName))
                {
                    _loggingService.AddLog("F9 key pressed. Starting...");
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() => StartCommand?.Execute(null)));
                }
            }
            else if (vkCode == vkF12)
            {
                if (IsRunning)
                {
                    _loggingService.AddLog("F12 key pressed. Shutting down...");
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() => StopCommand?.Execute(null)));
                }
            }
        };

        _stateManager.IsRunningChanged += OnIsRunningChanged;

        // --- Connect ShortcutResolver to our logs ---
        ShortcutResolver.LogAction = (msg) => _loggingService.AddLog(msg);

        IsAdmin = _privilegeService.IsAdministrator();
        EnableTaskbarAutoHide = _configService.IsTaskbarAutoHideEnabled();
        EnableDisplaySync = _configService.IsDisplaySyncEnabled();

        TargetProcessName = _configService.GetDefaultProcessName();
        CurrentImageFileName = _configService.GetBackgroundImageFileName();
        BackgroundColor = _configService.GetBackgroundColor();

        EnableBackgroundOverlay = _configService.IsBackgroundOverlayEnabled();
        BackgroundMode = _configService.GetBackgroundMode();
        ShouldShowExitTip = _configService.ShouldShowExitTip();

        AssociatedLaunchPath = _configService.GetAssociatedLaunchPath();
        LaunchOnAppStartup = _configService.IsLaunchOnAppStartupEnabled();
        LaunchOnTaskStart = _configService.IsLaunchOnTaskStartEnabled();
        AutoStartFromThirdParty = _configService.IsAutoStartFromThirdPartyEnabled();

        StartCommand = new RelayCommand(() => _ = OnStartAsync(), () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName));
        StopCommand = new RelayCommand(() => _ = OnStopAsync(), () => IsRunning);
        SelectImageCommand = new RelayCommand(SelectImage);
        ClearImageCommand = new RelayCommand(ClearImage, CanClearImage);
        RestartAsAdminCommand = new RelayCommand(OnRestartAsAdmin);
        AboutCommand = new RelayCommand(OnAbout);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

        SelectAssociatedProgramCommand = new RelayCommand(OnSelectAssociatedProgram);
    }

    public ICommand SelectAssociatedProgramCommand { get; }

    public void LaunchAssociatedProgram()
    {
        _launchService.Launch(AssociatedLaunchPath ?? "");
    }

    private void OnSelectAssociatedProgram()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Application or Shortcut",
            Filter = "Applications & Shortcuts|*.exe;*.lnk;*.url|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string path = openFileDialog.FileName;
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                AssociatedLaunchPath = ShortcutResolver.Resolve(path);
            }
            else
            {
                // Quote path if it contains spaces
                AssociatedLaunchPath = path.Contains(' ') ? $"\"{path}\"" : path;
            }
        }
    }

    private void OnAbout()
    {
        MessageBox.Show(
            "Responsive Window Tool\nVersion 1.2.0\n\nA modern UI powered by WebView2.\n\nGitHub: https://github.com/yumiaomaio/GameWindowTool", 
            "About", 
            MessageBoxButton.OK, 
            MessageBoxImage.Information);
    }

    private void OnIsRunningChanged(bool isRunning)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsRunning = isRunning;
        });
    }
    
    private async Task OnStartAsync()
    {
        if (TargetProcessName == null) return;
        try
        {
            await _stateManager.StartAsync(TargetProcessName);
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"Failed to start monitoring: {ex.Message}");
            MessageBox.Show($"An error occurred while starting the service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OnStopAsync()
    {
        try
        {
            await _stateManager.StopAsync();
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"Error during stop: {ex.Message}");
        }
    }

    private void OnRestartAsAdmin()
    {
        _privilegeService.RestartAsAdministrator();
    }

    private void SelectImage()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select a Background Image",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string sourcePath = openFileDialog.FileName;
            string fileName = Path.GetFileName(sourcePath);

            string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
            Directory.CreateDirectory(backgroundsDir);

            string destPath = Path.Combine(backgroundsDir, fileName);
            try
            {
                File.Copy(sourcePath, destPath, true);
                _configService.SetBackgroundMode(BackgroundMode.IMAGE);
                _configService.SetBackgroundImageFileName(fileName);
                BackgroundMode = BackgroundMode.IMAGE;
                CurrentImageFileName = fileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying file: {ex.Message}");
            }
        }
    }

    private void ClearImage()
    {
        _configService.SetBackgroundMode(BackgroundMode.SOLID_COLOR);
        _configService.SetBackgroundImageFileName(null);
        BackgroundMode = BackgroundMode.SOLID_COLOR;
        CurrentImageFileName = null;
    }

    private bool CanClearImage()
    {
        return !string.IsNullOrEmpty(CurrentImageFileName);
    }

    public void Dispose()
    {
        _stateManager.IsRunningChanged -= OnIsRunningChanged;
        if (_stateManager is IDisposable disposableManager)
        {
            disposableManager.Dispose();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
