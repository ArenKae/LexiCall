// Nœud de l'arbre latéral des catégories. Enveloppe soit une vraie catégorie,
// soit un nœud virtuel ("Toutes les entrées", "Sans catégorie") qui sert
// uniquement de filtre. La sélection remonte au MainWindowViewModel via un
// callback : le ViewModel ne référence jamais le TreeView directement.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.ViewModels;

public enum CategoryNodeKind
{
    AllEntries,
    Uncategorized,
    Category
}

public sealed class CategoryNodeViewModel : INotifyPropertyChanged
{
    private readonly Action<CategoryNodeViewModel> _onSelected;
    private int _entryCount;
    private int _colorIndex;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isEditing;
    private string _editName = string.Empty;

    private CategoryNodeViewModel(
        CategoryNodeKind kind,
        VocabularyCategory? category,
        Action<CategoryNodeViewModel> onSelected)
    {
        Kind = kind;
        Category = category;
        _onSelected = onSelected;
    }

    public static CategoryNodeViewModel CreateAllEntries(Action<CategoryNodeViewModel> onSelected) =>
        new(CategoryNodeKind.AllEntries, category: null, onSelected);

    public static CategoryNodeViewModel CreateUncategorized(Action<CategoryNodeViewModel> onSelected) =>
        new(CategoryNodeKind.Uncategorized, category: null, onSelected);

    public static CategoryNodeViewModel CreateForCategory(
        VocabularyCategory category,
        Action<CategoryNodeViewModel> onSelected) =>
        new(CategoryNodeKind.Category, category, onSelected);

    public event PropertyChangedEventHandler? PropertyChanged;

    public CategoryNodeKind Kind { get; }

    public VocabularyCategory? Category { get; }

    public bool IsVirtual => Kind != CategoryNodeKind.Category;

    public ObservableCollection<CategoryNodeViewModel> Children { get; } = [];

    public string DisplayName => Kind switch
    {
        CategoryNodeKind.AllEntries => "Toutes les entrées",
        CategoryNodeKind.Uncategorized => "Sans catégorie",
        _ => Category!.Name
    };

    // Null quand vide pour que WPF n'affiche pas d'infobulle.
    public string? DescriptionToolTip =>
        string.IsNullOrWhiteSpace(Category?.Description) ? null : Category.Description;

    public int EntryCount
    {
        get => _entryCount;
        set => SetProperty(ref _entryCount, value);
    }

    // Profondeur dans la hiérarchie (0 = racine ou nœud virtuel) : pilote
    // l'épaisseur des séparateurs et l'opacité du repère de couleur.
    public int Depth { get; set; }

    // Index de la catégorie racine dont ce nœud descend (partagé par toute la
    // sous-arborescence) : sert de graine à CategoryColorConverter.
    public int ColorIndex
    {
        get => _colorIndex;
        set => SetProperty(ref _colorIndex, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                if (Children.Count > 0)
                {
                    IsExpanded = true;
                }

                _onSelected(this);
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public void BeginEdit()
    {
        EditName = DisplayName;
        IsEditing = true;
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
