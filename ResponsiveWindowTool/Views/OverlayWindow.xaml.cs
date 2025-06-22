// File: Views/OverlayWindow.xaml.cs (Modified)
using System;
using System.IO;
using System.Windows;
using System.Windows.Media; // <-- 需要这个命名空间
using System.Windows.Media.Imaging;

namespace ResponsiveWindowTool.Views
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow(string? imagePath, string backgroundColor) // <-- 修改构造函数签名
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(imagePath))
            {
                // 图片优先
                try
                {
                    var uri = new Uri(imagePath, UriKind.Absolute);
                    BackgroundImage.Source = new BitmapImage(uri);
                }
                catch { /* Fallback to color if image fails */ }
            }
            else
            {
                // 如果没有图片，则设置背景色
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(backgroundColor);
                    this.Background = new SolidColorBrush(color);
                    // 确保图片控件是透明的，以免遮挡颜色
                    BackgroundImage.Source = null; 
                }
                catch { /* Fallback to default black if color string is invalid */ }
            }
        }
    }
}