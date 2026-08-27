// Code-behind for the AI enrichment review dialog — same pattern as
// EntryEditorWindow/CategoryEditorWindow: the ViewModel's Saved event closes
// the window with DialogResult = true, and the caller reads Result.
using System.Windows;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class EnrichmentReviewWindow : Window
{
    private readonly EnrichmentReviewWindowViewModel _viewModel;

    public EnrichmentReviewWindow(EnrichmentReviewWindowViewModel viewModel)
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

    public EnrichmentReviewResult? Result => _viewModel.Result;

    // Caps the window to 80% of the owner's size — Owner is only guaranteed
    // set by the time the window is shown, not at construction.
    private void EnrichmentReviewWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is null)
        {
            return;
        }

        Height = Math.Min(Height, Owner.ActualHeight * 0.8);
        Width = Math.Min(Width, Owner.ActualWidth * 0.8);
    }
}
