// File: ViewModels/MainViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using ResponsiveWindowTool.Services;

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
            set => SetProperty(ref _targetProcessName, value);
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

        public MainViewModel(ITargetStateManager stateManager, IConfigService configService)
        {
            _stateManager = stateManager;
            _configService = configService;

            _stateManager.IsRunningChanged += OnIsRunningChanged;

            TargetProcessName = _configService.GetDefaultProcessName();

            StartCommand = new RelayCommand(OnStart, () => !IsRunning && !string.IsNullOrWhiteSpace(TargetProcessName));
            StopCommand = new RelayCommand(OnStop, () => IsRunning);
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