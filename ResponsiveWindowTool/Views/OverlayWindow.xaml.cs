// File: Views/OverlayWindow.xaml.cs (Modified)
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ResponsiveWindowTool.Views
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow(string? imagePath) // <-- 构造函数接收图片路径
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    // 如果有有效路径，则加载图片
                    var uri = new Uri(imagePath, UriKind.Absolute);
                    BackgroundImage.Source = new BitmapImage(uri);
                }
                catch
                {
                    // 如果图片加载失败，则背景保持默认（黑色）
                }
            }
        }
    }
}