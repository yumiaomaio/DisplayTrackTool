// File: Views/MainWindow.xaml.cs
using System.Windows;
using ResponsiveWindowTool.ViewModels; // <-- 确保引用

namespace ResponsiveWindowTool.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel) // <-- 注入ViewModel
        {
            InitializeComponent();
            DataContext = viewModel; // <-- 设置DataContext
        }
    }
}