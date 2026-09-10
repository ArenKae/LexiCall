// ViewModel for the auto-categorization review dialog — a separate window
// from EnrichmentReviewWindow (see docs: the category suggestion shares no
// real shape with a field suggestion, forcing it into that window would mean
// static factories and a mostly-null result field for no actual reuse).
// Same paradigm as the other review window though: one card per suggestion,
// accept/correct/reject each, an Enregistrer that only ever exposes a Result —
// never persists anything itself.
using System.Collections.ObjectModel;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.ViewModels;

// NewCategoryName set = create a new category (with NewCategoryParentId/
// Description/IconGlyph); ExistingCategoryId set = attach to that one
// instead. Never both — CategorySuggestionCardViewModel.BuildResult() picks
// exactly one shape depending on IsNewCategory.
public sealed record CategorySuggestionResult(
    Guid? ExistingCategoryId,
    string? NewCategoryName,
    Guid? NewCategoryParentId,
    string? NewCategoryDescription,
    string? NewCategoryIconGlyph);

public sealed class CategorizationReviewWindowViewModel
{
    public CategorizationReviewWindowViewModel(
        IReadOnlyList<CategorizationSuggestion> suggestions,
        IReadOnlyList<VocabularyCategory> allCategories,
        IReadOnlyList<string> currentCategoryNames)
    {
        CategoryCards = new ObservableCollection<CategorySuggestionCardViewModel>(
            suggestions.Select(suggestion =>
                new CategorySuggestionCardViewModel(suggestion, allCategories, currentCategoryNames)));
        SaveCommand = new RelayCommand(Save);
    }

    public event EventHandler? Saved;

    public RelayCommand SaveCommand { get; }

    // Usually one; several when the word carries distinct senses across
    // different lexical fields ("ladre": leper / miser).
    public ObservableCollection<CategorySuggestionCardViewModel> CategoryCards { get; }

    // Only the cards the user actually accepted, in the order shown.
    public List<CategorySuggestionResult> Results { get; private set; } = [];

    private void Save()
    {
        Results = CategoryCards
            .Where(card => card.IsAccepted)
            .Select(card => card.BuildResult())
            .OfType<CategorySuggestionResult>()
            .ToList();

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
