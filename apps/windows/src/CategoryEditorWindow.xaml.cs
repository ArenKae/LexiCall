// Code-behind for the category editor window. Same pattern as
// EntryEditorWindow: SavedCategory is exposed to the caller, and the window
// closes when the ViewModel signals a valid category.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class CategoryEditorWindow : Window
{
    private readonly CategoryEditorWindowViewModel _viewModel;

    public CategoryEditorWindow(
        IEnumerable<VocabularyCategory> allCategories,
        VocabularyCategory? existingCategory = null,
        Guid? initialParentId = null)
    {
        _viewModel = new CategoryEditorWindowViewModel(allCategories, existingCategory, initialParentId);

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        _viewModel.CategorySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public VocabularyCategory? SavedCategory => _viewModel.SavedCategory;

    public string? SavedColorHex => _viewModel.SavedColorHex;

    private void ChooseIconButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new IconPickerWindow(_viewModel.IconGlyph)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedGlyph is not null)
        {
            _viewModel.IconGlyph = dialog.SelectedGlyph;
        }
    }

    private void ClearIconButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IconGlyph = string.Empty;
    }

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerWindow(_viewModel.ColorHex)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ColorHex = dialog.SelectedColorHex;
        }
    }

    private void ClearColorButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ColorHex = null;
    }
}
