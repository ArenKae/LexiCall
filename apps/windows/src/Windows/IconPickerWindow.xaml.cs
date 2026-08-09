// Code-behind for the icon picker. Same pattern as the other modal windows:
// the ViewModel signals the selection, the code-behind translates that into
// closing with DialogResult = true.
using System.Windows;
using System.Windows.Controls;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class IconPickerWindow : Window
{
    private readonly IconPickerWindowViewModel _viewModel;

    public IconPickerWindow(string? currentGlyph)
    {
        _viewModel = new IconPickerWindowViewModel(currentGlyph);

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        _viewModel.IconSelected += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        Loaded += (_, _) => SearchTextBox.Focus();
    }

    public string? SelectedGlyph => _viewModel.SelectedGlyph;

    private void IconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string glyph })
        {
            _viewModel.SelectIcon(glyph);
        }
    }
}
