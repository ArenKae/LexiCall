// One review card for a text-shaped enrichment suggestion (Définition,
// Synonymes, Exemples) in EnrichmentReviewWindow — Synonymes/Exemples arrive
// already formatted as editable text by the caller (TextListParser).
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LexiCall.Desktop.ViewModels;

public sealed class TextFieldSuggestionCardViewModel : INotifyPropertyChanged
{
    private bool _isAccepted = true;
    private string _editableText;

    public TextFieldSuggestionCardViewModel(
        string fieldLabel,
        string? currentValueDisplay,
        string? justification,
        string suggestedValue)
    {
        FieldLabel = fieldLabel;
        CurrentValueDisplay = currentValueDisplay;
        Justification = justification;
        _editableText = suggestedValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FieldLabel { get; }

    // Null when the field was empty — nothing to compare against.
    public string? CurrentValueDisplay { get; }

    public bool HasCurrentValue => !string.IsNullOrEmpty(CurrentValueDisplay);

    public string? Justification { get; }

    public bool HasJustification => !string.IsNullOrEmpty(Justification);

    // Pre-checked: unchecking is how the user rejects just this field.
    public bool IsAccepted
    {
        get => _isAccepted;
        set => SetProperty(ref _isAccepted, value);
    }

    // Editable even while accepted — this is how the user corrects a
    // suggestion instead of taking it verbatim.
    public string EditableText
    {
        get => _editableText;
        set => SetProperty(ref _editableText, value);
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
