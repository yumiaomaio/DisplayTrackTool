using System.Windows;
using System.Windows.Threading;

namespace ResponsiveWindowTool.Views;

public partial class ConfirmationDialog : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
        private TimeSpan _remainingTime;

        public ConfirmationDialog(string message, TimeSpan timeout)
        {
            InitializeComponent();

            MessageTextBlock.Text = message;
            _remainingTime = timeout;
            UpdateCountdownText(); // Initial text

            // 让按钮文本更通用或与新问题匹配
            KeepButton.Content = "Yes, Restore"; // 或者 "Restore"
            RevertButton.Content = "No, Keep Changes"; // 或者 "Keep"

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        // Public method for the caller to await the result
        public Task<bool> GetResultAsync()
        {
            return _tcs.Task;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _timer.Start(); // Start timer when window is loaded
            this.Activate(); // Try to bring window to front
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
            UpdateCountdownText();

            if (_remainingTime <= TimeSpan.Zero)
            {
                _timer.Stop();
                // 关键：超时现在意味着 "不恢复" (false)
                SetResultAndClose(false); 
            }
        }

        private void UpdateCountdownText()
        {
            // 改变倒计时的含义
            CountdownTextBlock.Text = $"Keeping changes in {(int)Math.Ceiling(_remainingTime.TotalSeconds)} seconds...";
        }

        private void KeepButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            // 用户点击 "Yes, Restore"，所以返回 true
            SetResultAndClose(true); 
        }

        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            // 用户点击 "No, Keep Changes"，所以返回 false
            SetResultAndClose(false); 
        }

        private void SetResultAndClose(bool result)
        {
            // TrySetResult ensures it's only set once
            _tcs.TrySetResult(result);
            this.Close();
        }

        // Optional: Handle window closing via other means (e.g., Alt+F4)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            // 手动关闭（如按对话框的ESC键），也视为 "不恢复" (false)
            _tcs.TrySetResult(false);
            base.OnClosing(e);
        }
        
    }