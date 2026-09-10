// Review card for the Type enrichment suggestion in EnrichmentReviewWindow —
// unlike the other three fields, this is a set of enum values, not free text.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class TypeSuggestionCardViewModel : INotifyPropertyChanged
{
    private bool _isAccepted = true;

    public TypeSuggestionCardViewModel(
        string? currentValueDisplay,
        string? justification,
        IEnumerable<VocabularyEntryType> suggestedTypes)
    {
        CurrentValueDisplay = currentValueDisplay;
        Justification = justification;
        TypeSelections = new TypeSelectionListViewModel(suggestedTypes);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Null when the current type was Undefined — nothing to compare against.
    public string? CurrentValueDisplay { get; }

    public bool HasCurrentValue => !string.IsNullOrEmpty(CurrentValueDisplay);

    public string? Justification { get; }

    public bool HasJustification => !string.IsNullOrEmpty(Justification);

    public bool IsAccepted
    {
        get => _isAccepted;
        set => SetProperty(ref _isAccepted, value);
    }

    // Pre-ticked with what was suggested, but every box stays editable — the
    // same "correction" affordance as the text fields' editable text.
    public TypeSelectionListViewModel TypeSelections { get; }

    public List<VocabularyEntryType> ToTypeList() => TypeSelections.ToTypeList();

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
