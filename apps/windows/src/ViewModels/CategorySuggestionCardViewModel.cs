// Review card for an auto-categorization suggestion (CategorizationReviewWindow)
// — unlike the field-suggestion cards, this one has no "value" of its own: it's
// a two-mode picker (attach to an existing category, or create a new one) with
// a side effect the card itself never performs (see BuildResult()).
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed record CategoryPickerOption(Guid Id, string Name, int Depth)
{
    // Sentinel for "no parent" in the parent picker — a real list item
    // instead of a null ComboBox entry, which would need special-casing in
    // every DataTemplate that renders the list.
    public static readonly CategoryPickerOption None = new(Guid.Empty, "Aucun (racine)", 0);
}

public sealed class CategorySuggestionCardViewModel : INotifyPropertyChanged
{
    private bool _isNewCategory;
    private CategoryPickerOption? _selectedExistingCategory;
    private string _newCategoryName;
    private CategoryPickerOption _selectedParentCategory = CategoryPickerOption.None;
    private string _newCategoryDescription = string.Empty;
    private string _newCategoryIconGlyph = string.Empty;

    public CategorySuggestionCardViewModel(
        CategorizationSuggestion suggestion,
        IReadOnlyList<VocabularyCategory> allCategories,
        IReadOnlyList<string> currentCategoryNames)
    {
        Justification = suggestion.Justification;
        CurrentCategoriesDisplay = currentCategoryNames.Count == 0 ? null : string.Join(", ", currentCategoryNames);

        ExistingCategoryOptions = CategoryHierarchy.Flatten(allCategories.ToList())
            .Select(item => new CategoryPickerOption(item.Category.Id, item.Category.Name, item.Depth))
            .ToList();
        ParentCategoryOptions = new[] { CategoryPickerOption.None }.Concat(ExistingCategoryOptions).ToList();

        _isNewCategory = suggestion.Decision == "new";
        _newCategoryName = suggestion.NewCategoryName ?? string.Empty;

        if (suggestion.Category is { } existing && Guid.TryParse(existing.Id, out var existingId))
        {
            _selectedExistingCategory = ExistingCategoryOptions.FirstOrDefault(option => option.Id == existingId);
        }

        if (suggestion.NewCategoryParent is { } parent && Guid.TryParse(parent.Id, out var parentId))
        {
            _selectedParentCategory = ParentCategoryOptions.FirstOrDefault(option => option.Id == parentId) ?? CategoryPickerOption.None;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Justification { get; }

    public bool HasJustification => !string.IsNullOrEmpty(Justification);

    // Info only, not editable here — attaching a category doesn't touch the
    // ones the entry already has (see CategoryIds: additive, never replaces).
    public string? CurrentCategoriesDisplay { get; }

    public bool HasCurrentCategories => !string.IsNullOrEmpty(CurrentCategoriesDisplay);

    public IReadOnlyList<CategoryPickerOption> ExistingCategoryOptions { get; }

    public IReadOnlyList<CategoryPickerOption> ParentCategoryOptions { get; }

    // Toggle between the two modes — the LLM's decision is just a starting
    // point, the user can flip it entirely (e.g. pick an existing category
    // even though "new" was suggested).
    public bool IsNewCategory
    {
        get => _isNewCategory;
        set => SetProperty(ref _isNewCategory, value);
    }

    public CategoryPickerOption? SelectedExistingCategory
    {
        get => _selectedExistingCategory;
        set => SetProperty(ref _selectedExistingCategory, value);
    }

    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    public CategoryPickerOption SelectedParentCategory
    {
        get => _selectedParentCategory;
        set => SetProperty(ref _selectedParentCategory, value);
    }

    // Only ever asked for in "new category" mode — the LLM never proposes
    // one, this is purely the user filling in what CategoryEditorWindow
    // would otherwise ask for later.
    public string NewCategoryDescription
    {
        get => _newCategoryDescription;
        set => SetProperty(ref _newCategoryDescription, value);
    }

    public string NewCategoryIconGlyph
    {
        get => _newCategoryIconGlyph;
        set
        {
            if (SetProperty(ref _newCategoryIconGlyph, value))
            {
                OnPropertyChanged(nameof(HasIcon));
                OnPropertyChanged(nameof(IconDisplayGlyph));
            }
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(NewCategoryIconGlyph);

    // Neutral glyph until an icon is chosen, so the picker button is never empty.
    public string IconDisplayGlyph => HasIcon ? NewCategoryIconGlyph : "🏷️";

    // Called by CategorizationReviewWindowViewModel.Save() — silently
    // returns null on an incomplete selection (mode "new" with an empty
    // name, or mode "existing" with nothing picked) rather than blocking the
    // whole dialog, consistent with the "when in doubt, apply nothing" bias
    // used elsewhere in the enrichment review.
    public CategorySuggestionResult? BuildResult()
    {
        if (IsNewCategory)
        {
            var name = NewCategoryName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var parentId = SelectedParentCategory.Id == Guid.Empty ? (Guid?)null : SelectedParentCategory.Id;
            return new CategorySuggestionResult(null, name, parentId, NewCategoryDescription.Trim(), NewCategoryIconGlyph);
        }

        return SelectedExistingCategory is { } selected
            ? new CategorySuggestionResult(selected.Id, null, null, null, null)
            : null;
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
