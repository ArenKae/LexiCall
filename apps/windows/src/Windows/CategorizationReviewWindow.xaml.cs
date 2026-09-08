// Code-behind for the auto-categorization review dialog — same pattern as
// EnrichmentReviewWindow: the ViewModel's Saved event closes the window with
// DialogResult = true, and the caller reads Result. Also owns the icon
// picker for the "new category" mode, copy of CategoryEditorWindow.xaml.cs
// (the card ViewModel knows no more about WPF than any other ViewModel here).
using System.Windows;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class CategorizationReviewWindow : Window
{
    private readonly CategorizationReviewWindowViewModel _viewModel;

    public CategorizationReviewWindow(CategorizationReviewWindowViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        _viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public IReadOnlyList<CategorySuggestionResult> Results => _viewModel.Results;

    // Caps the window to 80% of the owner's size — Owner is only guaranteed
    // set by the time the window is shown, not at construction.
    private void CategorizationReviewWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is null)
        {
            return;
        }

        Height = Math.Min(Height, Owner.ActualHeight * 0.8);
        Width = Math.Min(Width, Owner.ActualWidth * 0.8);
    }

    // Several cards can be on screen: the clicked button's DataContext is the
    // only thing saying which one owns the icon being picked.
    private void ChooseIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not CategorySuggestionCardViewModel card)
        {
            return;
        }

        var dialog = new IconPickerWindow(card.NewCategoryIconGlyph)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedGlyph is not null)
        {
            card.NewCategoryIconGlyph = dialog.SelectedGlyph;
        }
    }

    private void ClearIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is CategorySuggestionCardViewModel card)
        {
            card.NewCategoryIconGlyph = string.Empty;
        }
    }
}
