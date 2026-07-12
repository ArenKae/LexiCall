// ViewModel du formulaire d'ajout/modification d'une catégorie.
// Même pattern que EntryEditorWindowViewModel : validation locale, puis
// événement CategorySaved avec le résultat dans SavedCategory. Le sélecteur
// de parent exclut la catégorie éditée et ses descendantes pour empêcher
// la création d'un cycle.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
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
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CategorySaved;

    public RelayCommand SaveCategoryCommand { get; }

    public ObservableCollection<CategoryParentOption> ParentOptions { get; }

    public VocabularyCategory? SavedCategory { get; private set; }

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
            CreatedAt = _existingCategory?.CreatedAt ?? DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };

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
