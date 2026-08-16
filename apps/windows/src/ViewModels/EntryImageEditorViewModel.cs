// Small ViewModel for one image row in the entry form's picker.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;

namespace LexiCall.Desktop.ViewModels;

public sealed class EntryImageEditorViewModel : INotifyPropertyChanged
{
    private string _caption;

    public EntryImageEditorViewModel(
        Guid id,
        string caption,
        string imageBase64,
        Action<EntryImageEditorViewModel> onRemove)
    {
        Id = id;
        _caption = caption;
        ImageBase64 = imageBase64;
        RemoveCommand = new RelayCommand(() => onRemove(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; }

    public string ImageBase64 { get; }

    public string Caption
    {
        get => _caption;
        set => SetProperty(ref _caption, value);
    }

    public RelayCommand RemoveCommand { get; }

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
