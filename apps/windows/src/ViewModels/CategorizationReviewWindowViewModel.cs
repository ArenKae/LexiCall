// ViewModel for the auto-categorization review dialog — a separate window
// from EnrichmentReviewWindow (see docs: the category suggestion shares no
// real shape with a field suggestion, forcing it into that window would mean
// static factories and a mostly-null result field for no actual reuse).
// Same paradigm as the other review window though: one card, accept/correct/
// reject, an Enregistrer that only ever exposes a Result — never persists
// anything itself.
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
        CategorizationSuggestion suggestion,
        IReadOnlyList<VocabularyCategory> allCategories,
        IReadOnlyList<string> currentCategoryNames)
    {
        CategoryCard = new CategorySuggestionCardViewModel(suggestion, allCategories, currentCategoryNames);
        SaveCommand = new RelayCommand(Save);
    }

    public event EventHandler? Saved;

    public RelayCommand SaveCommand { get; }

    // Never null: a categorization suggestion always resolves to a decision
    // (existing or new), unlike the field-enrichment cards which may or may
    // not exist depending on what the API judged worth suggesting.
    public CategorySuggestionCardViewModel CategoryCard { get; }

    public CategorySuggestionResult? Result { get; private set; }

    private void Save()
    {
        Result = CategoryCard.BuildResult();
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
