// Small ViewModel for one image row in the entry form's picker.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;

namespace LexiCall.Desktop.ViewModels;

public sealed class EntryImageEditorViewModel : INotifyPropertyChanged
{
    private string _caption;
    private string _previewBase64;
    private EntryImageStatus _status;

    public EntryImageEditorViewModel(
        Guid id,
        string caption,
        string imageBase64,
        Action<EntryImageEditorViewModel> onRemove)
    {
        Id = id;
        _caption = caption;
        ImageBase64 = imageBase64;
        _previewBase64 = imageBase64;
        _status = imageBase64.Length > 0 ? EntryImageStatus.Ready : EntryImageStatus.Loading;
        RemoveCommand = new RelayCommand(() => onRemove(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; }

    // What the form saves back onto the entry: empty for an image this client
    // only knows by Id, so a downloaded one never lands in vocabulary.json.
    public string ImageBase64 { get; }

    // What the form displays, which also covers those downloaded images.
    public string PreviewBase64
    {
        get => _previewBase64;
        private set => SetProperty(ref _previewBase64, value);
    }

    public EntryImageStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // Re-encoded rather than kept as bytes so the picker binds through the
    // same converter as an inline image; a handful of thumbnails, once each.
    public void MarkLoaded(byte[]? bytes)
    {
        PreviewBase64 = bytes is null ? string.Empty : Convert.ToBase64String(bytes);
        Status = bytes is null ? EntryImageStatus.Failed : EntryImageStatus.Ready;
    }

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
