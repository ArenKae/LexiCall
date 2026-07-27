// ViewModel du formulaire d'ajout/modification d'une catégorie : validation
// locale puis événement CategorySaved avec le résultat dans SavedCategory.
// Le sélecteur de parent exclut la catégorie éditée et ses descendantes.
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

    // Couleur choisie au moment de l'enregistrement (null = automatique) :
    // séparée de SavedCategory, car ce n'est pas un champ du modèle de
    // catégorie — voir CategoryColorStore.
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

    // Repère neutre tant qu'aucune icône n'a été choisie, pour que le bouton
    // du sélecteur ne soit jamais vide.
    public string IconDisplayGlyph => HasIcon ? IconGlyph : "🏷️";

    // Null = pas de couleur choisie manuellement (couleur automatique).
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

    // Aperçu du bouton de sélection : la couleur choisie, ou la couleur
    // automatique de la catégorie existante (calculée en ignorant son propre
    // override, pour prévisualiser correctement un retour à "Automatique"),
    // ou un repère neutre pour une catégorie pas encore créée — son index de
    // couleur automatique n'existe qu'après le premier enregistrement.
    public SolidColorBrush ColorPreviewBrush
    {
        get
        {
            if (HasCustomColor && CategoryColorResolver.TryParseHex(ColorHex!, out var chosenColor))
            {
                return new SolidColorBrush(chosenColor);
            }

            if (_existingCategory is null)
            {
                return new SolidColorBrush(Colors.Transparent);
            }

            var overridesExcludingSelf = CategoryColorStore.LoadAll()
                .Where(pair => pair.Key != _existingCategory.Id)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var colorIndexes = CategoryHierarchy.ComputeColorIndexes(_allCategories);

            return new SolidColorBrush(
                CategoryColorResolver.Resolve(_existingCategory, _allCategories, colorIndexes, overridesExcludingSelf));
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
        // Une catégorie ne peut pas devenir son propre parent ni celui d'une de
        // ses descendantes : on retire tout son sous-arbre des options.
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

        foreach (var (category, depth) in CategoryHierarchy.Flatten(_allCategories))
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

        SavedCategory = new VocabularyCategory
        {
            Id = _existingCategory?.Id ?? Guid.NewGuid(),
            Name = name,
            ParentId = parentId,
            Description = Description.Trim(),
            IconGlyph = IconGlyph,
            CreatedAt = _existingCategory?.CreatedAt ?? DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
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
