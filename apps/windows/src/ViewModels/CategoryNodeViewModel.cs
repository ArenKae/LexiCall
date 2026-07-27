// Nœud de l'arbre latéral des catégories : une vraie catégorie ou un nœud
// virtuel filtre ("Toutes les entrées", "Sans catégorie"). La sélection
// remonte au MainWindowViewModel via callback, jamais via le TreeView.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
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
    private SolidColorBrush _colorBrush = new(Colors.Transparent);
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

    // Icône affichée devant le nom : emoji choisi pour la catégorie, ou un
    // repère par défaut (nœuds virtuels, ou catégorie sans icône).
    public string DisplayIcon => Kind switch
    {
        CategoryNodeKind.AllEntries => "📚",
        CategoryNodeKind.Uncategorized => "🏷️",
        _ => string.IsNullOrEmpty(Category!.IconGlyph) ? "🏷️" : Category.IconGlyph
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

    // Couleur effective de la catégorie (choisie manuellement ou dérivée
    // automatiquement de sa racine) : voir CategoryColorResolver.
    public SolidColorBrush ColorBrush
    {
        get => _colorBrush;
        set => SetProperty(ref _colorBrush, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    // Dépli/repli décidé uniquement par le clic (MainWindow.xaml.cs) ; la
    // sélection ne force pas IsExpanded, pour ne pas écraser un repli volontaire.
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
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
