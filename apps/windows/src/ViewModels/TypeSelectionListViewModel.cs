// The tickable list of grammatical types, shared by the entry form and the
// enrichment review's Type card. Nothing ticked is the untyped state, stored
// as [Undefined] — the API drops Undefined as soon as a real type is present,
// so the two never coexist.
using System.Collections.ObjectModel;
using System.ComponentModel;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class TypeSelectionListViewModel : INotifyPropertyChanged
{
    public TypeSelectionListViewModel(IEnumerable<VocabularyEntryType>? selected = null)
    {
        var initial = (selected ?? []).ToHashSet();

        Items = new ObservableCollection<TypeSelectionViewModel>(
            VocabularyEntryTypeCatalog.SelectableTypes
                .Select(option => new TypeSelectionViewModel(
                    option.Value,
                    option.Label,
                    initial.Contains(option.Value))));

        foreach (var item in Items)
        {
            item.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TypeSelectionViewModel> Items { get; }

    // What the closed dropdown shows, since the ticks themselves are hidden
    // until it is opened.
    public string SelectionSummary
    {
        get
        {
            var labels = Items.Where(item => item.IsSelected).Select(item => item.Label).ToList();
            return labels.Count > 0
                ? string.Join(", ", labels)
                : VocabularyEntryTypeCatalog.GetLabel(VocabularyEntryType.Undefined);
        }
    }

    public List<VocabularyEntryType> ToTypeList()
    {
        var types = Items
            .Where(item => item.IsSelected)
            .Select(item => item.Value)
            .ToList();

        return types.Count > 0 ? types : [VocabularyEntryType.Undefined];
    }

    public void Reset(IEnumerable<VocabularyEntryType> types)
    {
        var selected = types.ToHashSet();

        foreach (var item in Items)
        {
            item.IsSelected = selected.Contains(item.Value);
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
