// The editable list of senses behind a Definition, shared by EntryEditorWindow
// and the enrichment review's Définition card: one row per sense, added and
// removed freely, never dropping below one row.
using System.Collections.ObjectModel;
using System.ComponentModel;
using LexiCall.Desktop.Commands;

namespace LexiCall.Desktop.ViewModels;

public sealed class DefinitionSenseListViewModel
{
    public DefinitionSenseListViewModel(IEnumerable<string>? senses = null)
    {
        AddSenseCommand = new RelayCommand(() => Add(string.Empty));
        Reset(senses ?? []);
    }

    // Raised whenever a row changes (text typed, lock toggled), so an owner can
    // refresh what it derives from the rows.
    public event EventHandler? SenseChanged;

    public ObservableCollection<DefinitionSenseViewModel> Items { get; } = [];

    public RelayCommand AddSenseCommand { get; }

    public void Reset(IEnumerable<string> senses)
    {
        foreach (var sense in Items)
        {
            sense.PropertyChanged -= OnSensePropertyChanged;
        }

        Items.Clear();

        foreach (var sense in senses)
        {
            Add(sense);
        }

        if (Items.Count == 0)
        {
            Add(string.Empty);
        }
    }

    // Trimmed, empties dropped — the shape the model and the API both expect.
    public List<string> ToSenseList()
    {
        return Items
            .Select(sense => sense.Text.Trim())
            .Where(sense => sense.Length > 0)
            .ToList();
    }

    private void Add(string text)
    {
        var sense = new DefinitionSenseViewModel(text, Remove);
        sense.PropertyChanged += OnSensePropertyChanged;
        Items.Add(sense);
        RefreshCanRemove();
    }

    private void Remove(DefinitionSenseViewModel sense)
    {
        sense.PropertyChanged -= OnSensePropertyChanged;
        Items.Remove(sense);

        if (Items.Count == 0)
        {
            Add(string.Empty);
        }

        RefreshCanRemove();
    }

    private void RefreshCanRemove()
    {
        foreach (var sense in Items)
        {
            sense.CanRemove = Items.Count > 1;
        }
    }

    private void OnSensePropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        SenseChanged?.Invoke(this, EventArgs.Empty);
}
