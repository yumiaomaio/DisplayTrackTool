// File: ViewModels/MainViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using ResponsiveWindowTool.Services;
using Microsoft.Win32; // 新增：用于文件对话框
using System.IO;      // 新增：用于文件操作

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

        private string? _currentImageFileName;
        public string? CurrentImageFileName
        {
            get => _currentImageFileName;
            set => SetProperty(ref _currentImageFileName, value);
        }

        private double _portraitAspectRatio;
        public double PortraitAspectRatio
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

        public MainViewModel(ITargetStateManager stateManager, IConfigService configService)
        {
            _stateManager = stateManager;
            _configService = configService;

            _stateManager.IsRunningChanged += OnIsRunningChanged;

            TargetProcessName = _configService.GetDefaultProcessName();
            CurrentImageFileName = _configService.GetBackgroundImageFileName();
            PortraitAspectRatio = _configService.GetPortraitAspectRatio(); // 新增

            StartCommand = new RelayCommand(OnStart, () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName));
            StopCommand = new RelayCommand(OnStop, () => IsRunning);
            SelectImageCommand = new RelayCommand(SelectImage);
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

                // 1. 创建 Backgrounds 目录
                string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
                Directory.CreateDirectory(backgroundsDir);

                // 2. 复制文件
                string destPath = Path.Combine(backgroundsDir, fileName);
                try
                {
                    File.Copy(sourcePath, destPath, true); // 覆盖
                    // 3. 更新配置
                    _configService.SetBackgroundImageFileName(fileName);
                    // 4. 更新UI
                    CurrentImageFileName = fileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error copying file: {ex.Message}");
                }
            }
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