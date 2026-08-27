// Review card for the Type enrichment suggestion in EnrichmentReviewWindow —
// unlike the other three fields, this is an enum choice, not free text.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class TypeSuggestionCardViewModel : INotifyPropertyChanged
{
    private bool _isAccepted = true;
    private VocabularyEntryTypeOption? _selectedType;

    public TypeSuggestionCardViewModel(
        string? currentValueDisplay,
        string? justification,
        VocabularyEntryType suggestedType)
    {
        CurrentValueDisplay = currentValueDisplay;
        Justification = justification;
        _selectedType = VocabularyEntryTypeCatalog.All.FirstOrDefault(option => option.Value == suggestedType);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Null when the current type was Undefined — nothing to compare against.
    public string? CurrentValueDisplay { get; }

    public bool HasCurrentValue => !string.IsNullOrEmpty(CurrentValueDisplay);

    public string? Justification { get; }

    public bool HasJustification => !string.IsNullOrEmpty(Justification);

    public IReadOnlyList<VocabularyEntryTypeOption> AvailableTypes => VocabularyEntryTypeCatalog.All;

    public bool IsAccepted
    {
        get => _isAccepted;
        set => SetProperty(ref _isAccepted, value);
    }

    // Pre-selected to the suggested type, but still a real ComboBox — the
    // user can pick a different one before accepting, same "correction"
    // affordance as the text fields' editable text.
    public VocabularyEntryTypeOption? SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
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
