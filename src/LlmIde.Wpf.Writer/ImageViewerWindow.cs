using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Shows an image artifact, fit to the window while preserving its aspect ratio.
/// </summary>
public sealed class ImageViewerWindow : Window
{
    private ImageViewerWindow(string title, string imagePath, Window owner)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "이미지" : title;
        Owner = owner;
        Width = 820;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;

        BitmapImage bitmap = new BitmapImage();
        bitmap.BeginInit();
        // OnLoad so the file is not kept locked after loading.
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath);
        bitmap.EndInit();

        Content = new Image
        {
            Source = bitmap,
            Stretch = System.Windows.Media.Stretch.Uniform,
            Margin = new Thickness(8)
        };
    }

    /// <summary>
    /// Opens the image viewer for the given image file.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="imagePath">The absolute image file path.</param>
    /// <param name="owner">The owner window.</param>
    public static void Show(string title, string imagePath, Window owner)
    {
        new ImageViewerWindow(title, imagePath, owner).ShowDialog();
    }
}
