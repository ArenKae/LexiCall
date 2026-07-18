// Modale d'aperçu agrandi pour l'image d'une entrée : voir ImagePreviewWindow.xaml.
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow(Window owner, BitmapImage image)
    {
        InitializeComponent();
        Owner = owner;
        ThemeService.RegisterWindow(this);

        var workArea = SystemParameters.WorkArea;
        PreviewImage.MaxWidth = workArea.Width * 0.8;
        PreviewImage.MaxHeight = workArea.Height * 0.8;
        PreviewImage.Source = image;
    }

    private void PreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
