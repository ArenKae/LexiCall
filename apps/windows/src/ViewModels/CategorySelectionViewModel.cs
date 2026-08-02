// Small ViewModel for the entry form's category checklist. Decouples UI
// selection state from the VocabularyCategory model itself; Depth drives
// visual indentation.
using System.ComponentModel;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.ViewModels;

public sealed class CategorySelectionViewModel(VocabularyCategory category, bool isSelected, int depth)
    : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid CategoryId => category.Id;

    public string Name => category.Name;

    public int Depth { get; } = depth;

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
