// ViewModel for a category's icon (emoji) picker window: displays
// CategoryIconCatalog, filterable by keyword; clicking an icon fires
// IconSelected (same pattern as CategorySaved).
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class IconPickerGroupViewModel(string name, IReadOnlyList<CategoryIconOption> icons)
{
    public string Name { get; } = name;

    public IReadOnlyList<CategoryIconOption> Icons { get; } = icons;
}

public sealed class IconPickerWindowViewModel : INotifyPropertyChanged
{
    private string _searchQuery = string.Empty;

    public IconPickerWindowViewModel(string? currentGlyph)
    {
        SelectedGlyph = string.IsNullOrEmpty(currentGlyph) ? null : currentGlyph;
        Groups = [];
        RefreshGroups();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? IconSelected;

    public ObservableCollection<IconPickerGroupViewModel> Groups { get; }

    public string? SelectedGlyph { get; private set; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshGroups();
            }
        }
    }

    public void SelectIcon(string glyph)
    {
        SelectedGlyph = glyph;
        IconSelected?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshGroups()
    {
        Groups.Clear();
        var query = SearchQuery.Trim();

        foreach (var group in CategoryIconCatalog.Groups)
        {
            var icons = string.IsNullOrEmpty(query)
                ? group.Icons
                : group.Icons
                    .Where(icon => icon.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (icons.Count > 0)
            {
                Groups.Add(new IconPickerGroupViewModel(group.Name, icons));
            }
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
