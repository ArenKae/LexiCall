// Enlarged preview modal for an entry's image(s) — see ImagePreviewWindow.xaml.
using System.Windows;
using System.Windows.Input;
using LexiCall.Desktop.Converters;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.Windows;

public partial class ImagePreviewWindow : Window
{
    private readonly IReadOnlyList<EntryImage> _images;
    private int _currentIndex;

    public ImagePreviewWindow(Window owner, IReadOnlyList<EntryImage> images, int startIndex)
    {
        InitializeComponent();
        Owner = owner;
        ThemeService.RegisterWindow(this);

        _images = images;
        _currentIndex = startIndex;

        var workArea = SystemParameters.WorkArea;
        PreviewImage.MaxWidth = workArea.Width * 0.8;
        PreviewImage.MaxHeight = workArea.Height * 0.8;

        RefreshCurrentImage();
    }

    private void RefreshCurrentImage()
    {
        var current = _images[_currentIndex];
        PreviewImage.Source = Base64ImageConverter.ToBitmapImage(current.ImageBase64);
        CaptionText.Text = current.Caption;
        CaptionText.Visibility = string.IsNullOrWhiteSpace(current.Caption) ? Visibility.Collapsed : Visibility.Visible;

        PreviousButton.Visibility = _images.Count > 1 && _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = _images.Count > 1 && _currentIndex < _images.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowPrevious()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            RefreshCurrentImage();
        }
    }

    private void ShowNext()
    {
        if (_currentIndex < _images.Count - 1)
        {
            _currentIndex++;
            RefreshCurrentImage();
        }
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => ShowPrevious();

    private void NextButton_Click(object sender, RoutedEventArgs e) => ShowNext();

    private void PreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Left:
                ShowPrevious();
                break;
            case Key.Right:
                ShowNext();
                break;
        }
    }
}
