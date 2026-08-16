// ViewModel for the add/edit category form: local validation, then a
// CategorySaved event with the result in SavedCategory. The parent picker
// excludes the edited category and its descendants.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class CategoryParentOption(Guid? id, string displayName)
{
    public Guid? Id { get; } = id;

    public string DisplayName { get; } = displayName;
}

public sealed class CategoryEditorWindowViewModel : INotifyPropertyChanged
{
    private readonly List<VocabularyCategory> _allCategories;
    private readonly VocabularyCategory? _existingCategory;
    private readonly Guid _pendingCategoryId = Guid.NewGuid();
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _iconGlyph = string.Empty;
    private string? _colorHex;
    private string _errorMessage = string.Empty;
    private CategoryParentOption _selectedParentOption;

    public CategoryEditorWindowViewModel(
        IEnumerable<VocabularyCategory> allCategories,
        VocabularyCategory? existingCategory = null,
        Guid? initialParentId = null)
    {
        _allCategories = allCategories.ToList();
        _existingCategory = existingCategory;
        SaveCategoryCommand = new RelayCommand(SaveCategory);

        ParentOptions = new ObservableCollection<CategoryParentOption>(BuildParentOptions());

        var targetParentId = existingCategory?.ParentId ?? initialParentId;
        _selectedParentOption = ParentOptions.FirstOrDefault(option => option.Id == targetParentId)
            ?? ParentOptions[0];

        if (existingCategory is not null)
        {
            Name = existingCategory.Name;
            Description = existingCategory.Description;
            IconGlyph = existingCategory.IconGlyph;
            ColorHex = CategoryColorStore.LoadAll().TryGetValue(existingCategory.Id, out var hex) ? hex : null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CategorySaved;

    public RelayCommand SaveCategoryCommand { get; }

    public ObservableCollection<CategoryParentOption> ParentOptions { get; }

    public VocabularyCategory? SavedCategory { get; private set; }

    // Color chosen at save time (null = automatic) — kept separate from
    // SavedCategory since it isn't a field on the category model, see
    // CategoryColorStore.
    public string? SavedColorHex { get; private set; }

    public string WindowTitle => _existingCategory is null
        ? "Nouvelle catégorie"
        : "Modifier la catégorie";

    public string HeaderText => WindowTitle;

    public string DescriptionText => _existingCategory is null
        ? "Une catégorie peut être imbriquée sous une autre pour organiser ton vocabulaire."
        : "Modifie le nom, le parent ou la description de la catégorie.";

    public string SaveButtonText => _existingCategory is null
        ? "Créer"
        : "Enregistrer";

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                ClearError();
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string IconGlyph
    {
        get => _iconGlyph;
        set
        {
            if (SetProperty(ref _iconGlyph, value))
            {
                OnPropertyChanged(nameof(HasIcon));
                OnPropertyChanged(nameof(IconDisplayGlyph));
            }
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(IconGlyph);

    // Neutral glyph until an icon is chosen, so the picker button is never empty.
    public string IconDisplayGlyph => HasIcon ? IconGlyph : "🏷️";

    // Null = no manually chosen color (automatic).
    public string? ColorHex
    {
        get => _colorHex;
        set
        {
            if (SetProperty(ref _colorHex, value))
            {
                OnPropertyChanged(nameof(HasCustomColor));
                OnPropertyChanged(nameof(ColorPreviewBrush));
            }
        }
    }

    public bool HasCustomColor => !string.IsNullOrEmpty(ColorHex);

    public SolidColorBrush ColorPreviewBrush
    {
        get
        {
            if (HasCustomColor && CategoryColorResolver.TryParseHex(ColorHex!, out var chosenColor))
            {
                return new SolidColorBrush(chosenColor);
            }

            var previewCategory = _existingCategory ?? new VocabularyCategory
            {
                Id = _pendingCategoryId,
                Name = string.Empty,
                ParentId = SelectedParentOption.Id
            };

            var categoriesForPreview = _existingCategory is null
                ? _allCategories.Append(previewCategory).ToList()
                : _allCategories;

            var overridesExcludingSelf = CategoryColorStore.LoadAll()
                .Where(pair => pair.Key != previewCategory.Id)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var colorIndexes = CategoryHierarchy.ComputeColorIndexes(categoriesForPreview);

            return new SolidColorBrush(
                CategoryColorResolver.Resolve(previewCategory, categoriesForPreview, colorIndexes, overridesExcludingSelf));
        }
    }

    public CategoryParentOption SelectedParentOption
    {
        get => _selectedParentOption;
        set
        {
            if (SetProperty(ref _selectedParentOption, value))
            {
                ClearError();
                OnPropertyChanged(nameof(ColorPreviewBrush));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private List<CategoryParentOption> BuildParentOptions()
    {
        // A category can't become its own parent or one of its descendants':
        // its whole subtree is removed from the options.
        var excludedIds = new HashSet<Guid>();

        if (_existingCategory is not null)
        {
            excludedIds.Add(_existingCategory.Id);
            excludedIds.UnionWith(CategoryHierarchy.GetDescendantIds(_allCategories, _existingCategory.Id));
        }

        var options = new List<CategoryParentOption>
        {
            new(id: null, "(Aucun — catégorie racine)")
        };

        foreach (var (category, depth) in CategoryHierarchy.Flatten(_allCategories, CategoryOrderStore.LoadAll()))
        {
            if (!excludedIds.Contains(category.Id))
            {
                options.Add(new CategoryParentOption(
                    category.Id,
                    string.Concat(new string(' ', depth * 4), category.Name)));
            }
        }

        return options;
    }

    private void SaveCategory()
    {
        var name = Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Le nom est obligatoire.";
            return;
        }

        var parentId = SelectedParentOption.Id;

        var duplicateExists = _allCategories.Any(category =>
            category.Id != _existingCategory?.Id &&
            category.ParentId == parentId &&
            string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            ErrorMessage = "Une catégorie porte déjà ce nom au même niveau.";
            return;
        }

        var now = DateTimeOffset.Now;
        SavedCategory = new VocabularyCategory
        {
            Id = _existingCategory?.Id ?? _pendingCategoryId,
            Name = name,
            ParentId = parentId,
            Description = Description.Trim(),
            IconGlyph = IconGlyph,
            // Same instant on creation (CreatedAt == UpdatedAt) — a signal
            // relied on for sync-history's push/pull data-kind display.
            CreatedAt = _existingCategory?.CreatedAt ?? now,
            UpdatedAt = now
        };
        SavedColorHex = ColorHex;

        CategorySaved?.Invoke(this, EventArgs.Empty);
    }

    private void ClearError()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ErrorMessage = string.Empty;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}
