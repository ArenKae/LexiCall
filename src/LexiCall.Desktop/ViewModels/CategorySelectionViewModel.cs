// Petit ViewModel utilisé par la liste de cases à cocher du formulaire d'entrée.
// Il découple l'état de sélection UI du modèle VocabularyCategory lui-même.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.ViewModels;

public sealed class CategorySelectionViewModel(VocabularyCategory category, bool isSelected)
    : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid CategoryId => category.Id;

    public string Name => category.Name;

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
