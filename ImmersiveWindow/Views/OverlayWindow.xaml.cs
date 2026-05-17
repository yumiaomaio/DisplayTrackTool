// File: Views/OverlayWindow.xaml.cs (Modified)
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImmersiveWindow.Views;

public partial class OverlayWindow
{
    public OverlayWindow(string? imagePath, string backgroundColor)
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
            catch (Exception ex)
            {
                // Overlay window handles logging differently or via injection if needed, for now just removing the using System.Diagnostics and Debug.WriteLine
// or instead, let's keep it clean since it's a view without DI for now.
// Let's replace with empty or just System.Console.WriteLine.
System.Console.WriteLine($"[OverlayWindow] Failed to load background image '{imagePath}': {ex.Message}");
            }
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
            catch (Exception ex)
            {
                // Overlay window handles logging differently or via injection if needed, for now just removing the using System.Diagnostics and Debug.WriteLine
// or instead, let's keep it clean since it's a view without DI for now.
// Let's replace with empty or just System.Console.WriteLine.
System.Console.WriteLine($"[OverlayWindow] Failed to parse background color '{backgroundColor}': {ex.Message}. Falling back to black.");
            }
        }
    }
}