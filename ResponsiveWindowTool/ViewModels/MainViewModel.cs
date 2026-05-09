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
using ResponsiveWindowTool.Helpers;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ResponsiveWindowTool.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable, INotifyDataErrorInfo
    {
        private readonly ITargetStateManager _stateManager;
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;
        private readonly ILoggingService _loggingService;
        private readonly IPrivilegeService _privilegeService;

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
            set => SetProperty(ref _isAdmin, value);
        }

        public ObservableCollection<string> Logs => _loggingService.Logs;

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SelectImageCommand { get; }
        public ICommand ClearImageCommand { get; }
        public ICommand RestartAsAdminCommand { get; }

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
                    ValidateAspectRatio(value);
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

        #region Validation
        private readonly Dictionary<string, List<string>> _errors = new();
        public bool HasErrors => _errors.Count > 0;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || !_errors.ContainsKey(propertyName))
                return Enumerable.Empty<string>();
            return _errors[propertyName];
        }

        private void ValidateAspectRatio(string? value)
        {
            const string propertyName = nameof(PortraitAspectRatio);
            if (string.IsNullOrWhiteSpace(value))
            {
                ClearErrors(propertyName);
                return;
            }

            var parts = value.Split('/');
            bool isValid = false;
            if (parts.Length == 2)
            {
                if (double.TryParse(parts[0].Trim(), out double n) && 
                    double.TryParse(parts[1].Trim(), out double d) && d != 0)
                {
                    isValid = true;
                }
            }

            if (!isValid)
            {
                AddError(propertyName, "Invalid aspect ratio. Use format 'Width/Height' (e.g., 9/16).");
            }
            else
            {
                ClearErrors(propertyName);
            }
        }

        private void AddError(string propertyName, string error)
        {
            if (!_errors.ContainsKey(propertyName))
                _errors[propertyName] = new List<string>();

            if (!_errors[propertyName].Contains(error))
            {
                _errors[propertyName].Add(error);
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        private void ClearErrors(string propertyName)
        {
            if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                OnPropertyChanged(nameof(HasErrors));
            }
        }
        #endregion

        public MainViewModel(
            ITargetStateManager stateManager, 
            IConfigService configService, 
            IDialogService dialogService,
            ILoggingService loggingService,
            IPrivilegeService privilegeService)
        {
            _stateManager = stateManager;
            _configService = configService;
            _dialogService = dialogService;
            _loggingService = loggingService;
            _privilegeService = privilegeService;

            _stateManager.IsRunningChanged += OnIsRunningChanged;

            IsAdmin = _privilegeService.IsAdministrator();

            TargetProcessName = _configService.GetDefaultProcessName();
            CurrentImageFileName = _configService.GetBackgroundImageFileName();
            PortraitAspectRatio = _configService.GetPortraitAspectRatio();

            EnableBackgroundOverlay = _configService.IsBackgroundOverlayEnabled();

            StartCommand = new RelayCommand(async () => await OnStartAsync(), () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName) && !HasErrors);
            StopCommand = new RelayCommand(async () => await OnStopAsync(), () => IsRunning);
            SelectImageCommand = new RelayCommand(SelectImage);
            ClearImageCommand = new RelayCommand(ClearImage, CanClearImage);
            RestartAsAdminCommand = new RelayCommand(OnRestartAsAdmin);
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
            await _stateManager.StartAsync(TargetProcessName);
        }

        private async Task OnStopAsync()
        {
            await _stateManager.StopAsync();
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