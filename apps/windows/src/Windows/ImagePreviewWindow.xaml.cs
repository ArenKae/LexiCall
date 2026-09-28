// Enlarged preview modal for an entry's image(s) — see ImagePreviewWindow.xaml.

using System.Windows;
using System.Windows.Input;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class ImagePreviewWindow : Window
{
    private readonly IReadOnlyList<EntryImageViewModel> _images;
    private int _currentIndex;

    // Opened only with images whose bytes are already decoded, so navigating
    // between them never shows an empty frame.
    public ImagePreviewWindow(Window owner, IReadOnlyList<EntryImageViewModel> images, int startIndex)
    {
        InitializeComponent();
        Owner = owner;
        ThemeService.RegisterWindow(this);

        _images = images;
        _currentIndex = startIndex;

        // Capped against the owner, not the screen work area: that area covers
        // the primary monitor in logical units, which overflows the actual
        // screen once the app sits on a smaller or differently scaled one. The
        // height leaves room for the title bar, caption and Fermer button, so
        // the whole modal still fits inside the owner.
        PreviewImage.MaxWidth = owner.ActualWidth * 0.8;
        PreviewImage.MaxHeight = owner.ActualHeight * 0.7;

        RefreshCurrentImage();
    }

    private void RefreshCurrentImage()
    {
        var current = _images[_currentIndex];
        // Decoded at full size here, not reused from the card: the thumbnail
        // behind it is decoded small on purpose and would show blurred.
        PreviewImage.Source = current.CreateFullSizeImage() ?? current.Image;
        CaptionText.Text = current.Caption;
        CaptionText.Visibility = string.IsNullOrWhiteSpace(current.Caption) ? Visibility.Collapsed : Visibility.Visible;

        PreviousButton.Visibility = _images.Count > 1 && _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = _images.Count > 1 && _currentIndex < _images.Count - 1 ? Visibility.Visible : Visibility.Collapsed;

        // Each image resizes the window, which would otherwise keep its
        // top-left corner and drift off-centre as the format changes.
        WindowLayoutService.CenterOnOwner(this);
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
