// ViewModel de la fenêtre de gestion des catégories.
// Les changements sont d'abord appliqués à une copie locale ; ils ne remplacent
// les catégories de l'application que lorsque l'utilisateur clique sur Valider.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.ViewModels;

public sealed class CategoriesWindowViewModel : INotifyPropertyChanged
{
    private readonly HashSet<Guid> _usedCategoryIds;
    private VocabularyCategory? _selectedCategory;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;

    public CategoriesWindowViewModel(
        IEnumerable<VocabularyCategory> categories,
        IEnumerable<VocabularyEntry> entries)
    {
        Categories = new ObservableCollection<VocabularyCategory>(
            categories
                .Select(CloneCategory)
                .OrderBy(category => category.Name));

        // On garde la liste des catégories utilisées pour empêcher une suppression
        // qui laisserait des entrées pointer vers une catégorie inexistante.
        _usedCategoryIds = entries
            .SelectMany(entry => entry.CategoryIds)
            .ToHashSet();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<VocabularyCategory> Categories { get; }

    public IReadOnlyList<VocabularyCategory> SavedCategories => Categories.ToList();

    public VocabularyCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                Name = value?.Name ?? string.Empty;
                Description = value?.Description ?? string.Empty;
                ErrorMessage = string.Empty;
            }
        }
    }

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

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearForm()
    {
        SelectedCategory = null;
        Name = string.Empty;
        Description = string.Empty;
        ErrorMessage = string.Empty;
    }

    public bool AddCategory()
    {
        var name = Name.Trim();

        if (!ValidateCategoryName(name, null))
        {
            return false;
        }

        var now = DateTimeOffset.Now;
        var category = new VocabularyCategory
        {
            Name = name,
            Description = Description.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        Categories.Add(category);
        SortCategories();
        SelectedCategory = category;
        return true;
    }

    public bool UpdateSelectedCategory()
    {
        if (SelectedCategory is null)
        {
            ErrorMessage = "Sélectionne une catégorie à modifier.";
            return false;
        }

        var name = Name.Trim();

        if (!ValidateCategoryName(name, SelectedCategory.Id))
        {
            return false;
        }

        var index = Categories
            .Select((category, categoryIndex) => new { category, categoryIndex })
            .FirstOrDefault(item => item.category.Id == SelectedCategory.Id)
            ?.categoryIndex;

        if (index is null)
        {
            ErrorMessage = "La catégorie sélectionnée est introuvable.";
            return false;
        }

        var updatedCategory = new VocabularyCategory
        {
            Id = SelectedCategory.Id,
            Name = name,
            ParentId = SelectedCategory.ParentId,
            Description = Description.Trim(),
            CreatedAt = SelectedCategory.CreatedAt,
            UpdatedAt = DateTimeOffset.Now
        };

        Categories[index.Value] = updatedCategory;
        SortCategories();
        SelectedCategory = updatedCategory;
        return true;
    }

    public bool DeleteSelectedCategory()
    {
        if (SelectedCategory is null)
        {
            ErrorMessage = "Sélectionne une catégorie à supprimer.";
            return false;
        }

        if (_usedCategoryIds.Contains(SelectedCategory.Id))
        {
            ErrorMessage = "Impossible de supprimer une catégorie utilisée par des mots.";
            return false;
        }

        if (Categories.Any(category => category.ParentId == SelectedCategory.Id))
        {
            ErrorMessage = "Impossible de supprimer une catégorie qui contient des sous-catégories.";
            return false;
        }

        Categories.Remove(SelectedCategory);
        ClearForm();
        return true;
    }

    public bool SavePendingChanges()
    {
        // Valider doit être confortable : si l'utilisateur vient de saisir une
        // nouvelle catégorie, on l'ajoute avant de fermer la fenêtre.
        if (SelectedCategory is null)
        {
            return string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Description)
                || AddCategory();
        }

        if (IsSelectedCategoryUnchanged())
        {
            return true;
        }

        return UpdateSelectedCategory();
    }

    private bool ValidateCategoryName(string name, Guid? existingCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Le nom de la catégorie est obligatoire.";
            return false;
        }

        var duplicateExists = Categories.Any(category =>
            category.Id != existingCategoryId &&
            category.ParentId is null &&
            string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            ErrorMessage = "Une catégorie avec ce nom existe déjà.";
            return false;
        }

        return true;
    }

    private void SortCategories()
    {
        var sortedCategories = Categories
            .OrderBy(category => category.Name)
            .ToList();

        Categories.Clear();

        foreach (var category in sortedCategories)
        {
            Categories.Add(category);
        }
    }

    private void ClearError()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ErrorMessage = string.Empty;
        }
    }

    private bool IsSelectedCategoryUnchanged()
    {
        return SelectedCategory is not null &&
            string.Equals(SelectedCategory.Name, Name.Trim(), StringComparison.Ordinal) &&
            string.Equals(SelectedCategory.Description, Description.Trim(), StringComparison.Ordinal);
    }

    private static VocabularyCategory CloneCategory(VocabularyCategory category)
    {
        return new VocabularyCategory
        {
            Id = category.Id,
            Name = category.Name,
            ParentId = category.ParentId,
            Description = category.Description,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
