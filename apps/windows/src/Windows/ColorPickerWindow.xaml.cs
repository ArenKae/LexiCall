// Code-behind for the color picker. Same pattern as IconPickerWindow: the
// ViewModel signals the selection, the code-behind translates that into
// closing with DialogResult = true.
using System.Windows;
using System.Windows.Controls;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class ColorPickerWindow : Window
{
    private readonly ColorPickerWindowViewModel _viewModel;

    public ColorPickerWindow(string? currentColorHex)
    {
        _viewModel = new ColorPickerWindowViewModel(currentColorHex);

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        _viewModel.ColorSelected += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public string? SelectedColorHex => _viewModel.SelectedColorHex;

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string colorHex })
        {
            _viewModel.SelectColor(colorHex);
        }
    }

    private void AutomaticButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectColor(null);
    }
}
