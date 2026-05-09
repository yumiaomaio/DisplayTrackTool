// File: ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using ResponsiveWindowTool.Services;
using Microsoft.Win32;
using System.IO;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
    
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ITargetStateManager _stateManager;
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;

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

        public ObservableCollection<string> Logs => _stateManager.Logs;

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SelectImageCommand { get; }
        public ICommand ClearImageCommand { get; }

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

        private string? _portraitAspectRatio;
        public string? PortraitAspectRatio
        {
            get => _portraitAspectRatio;
            set
            {
                if (SetProperty(ref _portraitAspectRatio, value))
                {
                    _configService.SetPortraitAspectRatio(value);
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
        #endregion

        public MainViewModel(ITargetStateManager stateManager, IConfigService configService, IDialogService dialogService)
        {
            _stateManager = stateManager;
            _configService = configService;
            _dialogService = dialogService;

            _stateManager.IsRunningChanged += OnIsRunningChanged;

            TargetProcessName = _configService.GetDefaultProcessName();
            CurrentImageFileName = _configService.GetBackgroundImageFileName();
            PortraitAspectRatio = _configService.GetPortraitAspectRatio();

            EnableBackgroundOverlay = _configService.IsBackgroundOverlayEnabled();

            StartCommand = new RelayCommand(OnStart, () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName));
            StopCommand = new RelayCommand(OnStop, () => IsRunning);
            SelectImageCommand = new RelayCommand(SelectImage);
            ClearImageCommand = new RelayCommand(ClearImage, CanClearImage);
        }

        private void OnIsRunningChanged(bool isRunning)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRunning = isRunning;
            });
        }
        
        private void OnStart()
        {
            if (TargetProcessName == null) return;
            _stateManager.Start(TargetProcessName);
        }

        private void OnStop()
        {
            _stateManager.Stop();
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
                    _configService.SetBackgroundMode(BackgroundMode.Image);
                    _configService.SetBackgroundImageFileName(fileName);
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
            _configService.SetBackgroundMode(BackgroundMode.SolidColor);
            _configService.SetBackgroundImageFileName(null);
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
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}