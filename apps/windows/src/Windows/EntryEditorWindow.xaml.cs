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

    private readonly Func<VocabularyCategory, string?>? _saveCategory;

    // Enrichment/categorization requests outlive the window when the user
    // cancels before the response arrives (Annuler closes the window
    // immediately, the HTTP call keeps running) — without this guard, the
    // completion handler opens a review dialog with Owner set to an
    // already-closed window, which WPF throws on and crashes the app.
    private bool _isClosed;

    public EntryEditorWindow(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null,
        Guid? initialCategoryId = null,
        VocabularyApiClient? apiClient = null,
        Func<VocabularyCategory, string?>? saveCategory = null)
    {
        _viewModel = new EntryEditorWindowViewModel(existingEntry, availableCategories, initialCategoryId, apiClient);
        _saveCategory = saveCategory;

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);
        ClickAwayPopup.Register(EntryTypePopup);

        // The ViewModel knows nothing about WPF: it raises a plain business
        // event that the window translates into a DialogResult.
        _viewModel.EntrySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        _viewModel.EnrichmentSuggestionsReady += (_, _) => ShowEnrichmentReview();
        _viewModel.CategorizationSuggestionsReady += (_, _) => ShowCategorizationReview();

        Closed += (_, _) => _isClosed = true;
    }

    public VocabularyEntry? SavedEntry => _viewModel.SavedEntry;

    private void ShowEnrichmentReview()
    {
        if (_isClosed)
        {
            return;
        }

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
            _viewModel.DefinitionSenses.ToSenseList(),
            _viewModel.TypeSelections.ToTypeList(),
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

    private void ShowCategorizationReview()
    {
        if (_isClosed)
        {
            return;
        }

        if (_viewModel.PendingCategorizationSuggestions is not { } suggestions)
        {
            return;
        }

        if (!suggestions.WordRecognized)
        {
            AlertDialog.Show(
                this,
                $"« {_viewModel.Word} » n'a pas été reconnu comme un mot ou une expression française existante — aucune catégorie n'a été proposée.",
                "Catégorisation automatique");
            return;
        }

        if (suggestions.Suggestions.Count == 0)
        {
            AlertDialog.Show(this, "Aucune suggestion de catégorie pour cette entrée.", "Catégorisation automatique");
            return;
        }

        var currentCategoryNames = _viewModel.CategorySelections
            .Where(category => category.IsSelected)
            .Select(category => category.Name)
            .ToList();

        var reviewViewModel = new CategorizationReviewWindowViewModel(
            suggestions.Suggestions, _viewModel.AvailableCategories, currentCategoryNames);
        var dialog = new CategorizationReviewWindow(reviewViewModel) { Owner = this };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var result in reviewViewModel.Results)
        {
            VocabularyCategory? createdCategory = null;

            if (result.NewCategoryName is { } newName)
            {
                var newCategory = new VocabularyCategory
                {
                    Id = Guid.NewGuid(),
                    Name = newName,
                    ParentId = result.NewCategoryParentId,
                    Description = result.NewCategoryDescription ?? string.Empty,
                    IconGlyph = result.NewCategoryIconGlyph ?? string.Empty,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };

                var error = _saveCategory?.Invoke(newCategory);
                if (error is not null)
                {
                    AlertDialog.Show(this, error, "Création de catégorie impossible");
                    continue;
                }

                createdCategory = newCategory;
            }

            _viewModel.ApplyCategorization(result, createdCategory);
        }
    }

    // Caps the window to 80% of the owner's size — Owner is only guaranteed
    // set by the time the window is shown, not at construction.
    private void EntryEditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CategorySelections.FirstOrDefault(category => category.IsSelected) is { } firstSelected)
        {
            CategoryList.ScrollIntoView(firstSelected);
        }

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
