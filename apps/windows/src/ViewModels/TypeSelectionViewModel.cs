// Small ViewModel for a grammatical-type checkbox, in the entry form and in
// the enrichment review's Type card. Mirrors CategorySelectionViewModel:
// selection state kept out of the model itself.
using System.ComponentModel;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.ViewModels;

public sealed class TypeSelectionViewModel(VocabularyEntryType value, string label, bool isSelected)
    : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public VocabularyEntryType Value { get; } = value;

    public string Label { get; } = label;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
