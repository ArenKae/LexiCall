// One editable sense of a definition — a single row in the sense list shown by
// EntryEditorWindow and by the enrichment review's Définition card.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;

namespace LexiCall.Desktop.ViewModels;

public sealed class DefinitionSenseViewModel : INotifyPropertyChanged
{
    private string _text;
    private bool _canRemove;
    private bool _isRephraseLocked;

    public DefinitionSenseViewModel(string text, Action<DefinitionSenseViewModel> onRemove)
    {
        _text = text;
        RemoveCommand = new RelayCommand(() => onRemove(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    // False on the last remaining row: a definition always keeps at least one
    // sense, so its ✕ is hidden rather than disabled.
    public bool CanRemove
    {
        get => _canRemove;
        set => SetProperty(ref _canRemove, value);
    }

    public RelayCommand RemoveCommand { get; }

    // Review card only: a locked sense is left out of the "Reformuler" request
    // and keeps its text.
    public bool IsRephraseLocked
    {
        get => _isRephraseLocked;
        set => SetProperty(ref _isRephraseLocked, value);
    }

    // Review card only: the wording sent to every rephrase call for this sense,
    // captured on the first one — reusing the latest result instead would drift
    // further from the original meaning with each successive rephrase.
    public string? RephraseAnchor { get; set; }

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
