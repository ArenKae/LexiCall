// Code-behind for the entry editor window. Exposes SavedEntry to the caller
// and closes when the ViewModel signals a valid entry was built.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class EntryEditorWindow : Window
{
    private readonly EntryEditorWindowViewModel _viewModel;

    public EntryEditorWindow(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null,
        Guid? initialCategoryId = null,
        VocabularyApiClient? apiClient = null)
    {
        _viewModel = new EntryEditorWindowViewModel(existingEntry, availableCategories, initialCategoryId, apiClient);

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        // The ViewModel knows nothing about WPF: it raises a plain business
        // event that the window translates into a DialogResult.
        _viewModel.EntrySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        // Same reasoning: the ViewModel can't open EnrichmentReviewWindow
        // itself, so it just signals "suggestions are ready" and the window
        // does the rest, then reports back via ApplyEnrichmentResult.
        _viewModel.EnrichmentSuggestionsReady += (_, _) => ShowEnrichmentReview();
    }

    public VocabularyEntry? SavedEntry => _viewModel.SavedEntry;

    private void ShowEnrichmentReview()
    {
        if (_viewModel.PendingEnrichmentSuggestions is not { } suggestions ||
            _viewModel.ApiClient is not { } apiClient)
        {
            return;
        }

        if (!suggestions.WordRecognized)
        {
            AlertDialog.Show(
                this,
                $"« {_viewModel.Word} » n'a pas été reconnu comme un mot ou une expression française existante — aucune suggestion n'a pu être générée.",
                "Enrichissement IA");
            return;
        }

        var reviewViewModel = new EnrichmentReviewWindowViewModel(
            _viewModel.Word,
            _viewModel.Definition,
            _viewModel.Type,
            TextListParser.ParseCommaSeparatedText(_viewModel.SynonymsText),
            TextListParser.ParseLineSeparatedText(_viewModel.ExampleSentencesText),
            suggestions,
            apiClient);

        if (!reviewViewModel.HasAnySuggestion)
        {
            AlertDialog.Show(this, "Aucune suggestion : tous les champs sont verrouillés ou déjà jugés satisfaisants.", "Enrichissement IA");
            return;
        }

        var dialog = new EnrichmentReviewWindow(reviewViewModel) { Owner = this };

        if (dialog.ShowDialog() == true && reviewViewModel.Result is not null)
        {
            _viewModel.ApplyEnrichmentResult(reviewViewModel.Result);
        }
    }

    // Caps the window to 80% of the owner's size — Owner is only guaranteed
    // set by the time the window is shown, not at construction.
    private void EntryEditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is null)
        {
            return;
        }

        Height = Math.Min(Height, Owner.ActualHeight * 0.8);
        Width = Math.Min(Width, Owner.ActualWidth * 0.8);
    }

    // The file picker is a WPF detail: the ViewModel only receives the chosen
    // paths and knows nothing about OpenFileDialog.
    private void AddImagesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.AddImagesFromFiles(dialog.FileNames);
        }
    }
}
