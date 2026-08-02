// A node in the sidebar category tree: a real category or a virtual filter
// node ("Toutes les entrées", "Sans catégorie"). Selection flows up to
// MainWindowViewModel via callback, never through the TreeView directly.
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
    private bool _canMoveUp;
    private bool _canMoveDown;

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

    // Icon shown before the name: the category's chosen emoji, or a default
    // glyph (virtual nodes, or a category with no icon).
    public string DisplayIcon => Kind switch
    {
        CategoryNodeKind.AllEntries => "📚",
        CategoryNodeKind.Uncategorized => "🏷️",
        _ => string.IsNullOrEmpty(Category!.IconGlyph) ? "🏷️" : Category.IconGlyph
    };

    // Null when empty so WPF doesn't render a tooltip at all.
    public string? DescriptionToolTip =>
        string.IsNullOrWhiteSpace(Category?.Description) ? null : Category.Description;

    public int EntryCount
    {
        get => _entryCount;
        set => SetProperty(ref _entryCount, value);
    }

    // Depth in the hierarchy (0 = root or virtual node): drives separator
    // thickness and the color marker's opacity.
    public int Depth { get; set; }

    // Effective category color (manually chosen, or derived automatically
    // from its root) — see CategoryColorResolver.
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

    // Expand/collapse is driven only by the click (MainWindow.xaml.cs);
    // selection never forces IsExpanded, so it doesn't override a
    // deliberate collapse.
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

    // Recomputed when the context menu opens (MainWindow.xaml.cs), to
    // disable "Monter"/"Descendre" (Move up/down) at either end of a
    // sibling group.
    public bool CanMoveUp
    {
        get => _canMoveUp;
        set => SetProperty(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        set => SetProperty(ref _canMoveDown, value);
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
