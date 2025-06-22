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

        // 新增：手动触发 CanExecuteChanged
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
    
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ITargetStateManager _stateManager;
        private readonly IConfigService _configService;

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
        public ICommand ClearImageCommand { get; } // 新增

        private string? _currentImageFileName;
        public string? CurrentImageFileName
        {
            get => _currentImageFileName;
            set
            {
                if (SetProperty(ref _currentImageFileName, value))
                {
                    // 通知 "Clear" 按钮刷新其可用状态
                    (ClearImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // 将属性类型从 double 改为 string?
        private string? _portraitAspectRatio;
        public string? PortraitAspectRatio
        {
            get => _portraitAspectRatio;
            set
            {
                if (SetProperty(ref _portraitAspectRatio, value))
                {
                    // 直接将用户输入的字符串保存到配置
                    _configService.SetPortraitAspectRatio(value);
                }
            }
        }

        public MainViewModel(ITargetStateManager stateManager, IConfigService configService)
        {
            _stateManager = stateManager;
            _configService = configService;

            _stateManager.IsRunningChanged += OnIsRunningChanged;

            TargetProcessName = _configService.GetDefaultProcessName();
            CurrentImageFileName = _configService.GetBackgroundImageFileName();
            PortraitAspectRatio = _configService.GetPortraitAspectRatio();

            StartCommand = new RelayCommand(OnStart, () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName));
            StopCommand = new RelayCommand(OnStop, () => IsRunning);
            SelectImageCommand = new RelayCommand(SelectImage);
            ClearImageCommand = new RelayCommand(ClearImage, CanClearImage); // 新增
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
                    // 关键：当用户选择图片时，模式自动切换为Image
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

        // 新增：清除图片
        private void ClearImage()
        {
            // 关键：当用户清除图片时，模式自动切换回SolidColor
            _configService.SetBackgroundMode(BackgroundMode.SolidColor);
            _configService.SetBackgroundImageFileName(null);
            CurrentImageFileName = null;
        }

        // 新增：判断是否可以清除图片
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