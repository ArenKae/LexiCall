// One image of the selected entry in the Détails card: the decoded thumbnail
// plus its load state, since an entry received from a pull carries image
// metadata only and its bytes arrive from the API a moment later.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using LexiCall.Desktop.Converters;

namespace LexiCall.Desktop.ViewModels;

public enum EntryImageStatus
{
    Loading,
    Ready,
    Failed
}

public sealed class EntryImageViewModel : INotifyPropertyChanged
{
    private BitmapImage? _image;
    private byte[]? _sourceBytes;
    private EntryImageStatus _status = EntryImageStatus.Loading;

    public EntryImageViewModel(Guid id, string caption)
    {
        Id = id;
        Caption = caption;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; }

    public string Caption { get; }

    public BitmapImage? Image
    {
        get => _image;
        private set => SetProperty(ref _image, value);
    }

    public EntryImageStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public void MarkLoading()
    {
        _sourceBytes = null;
        Image = null;
        Status = EntryImageStatus.Loading;
    }

    // Null bytes are a download that failed, and bytes WPF can't decode leave
    // a null bitmap: both land on the retryable failed state. The compressed
    // bytes are kept (60-150 KB) so the enlarged preview can decode at full
    // size without the thumbnail paying for it.
    public void MarkLoaded(byte[]? bytes)
    {
        var thumbnail = bytes is null
            ? null
            : Base64ImageConverter.ToBitmapImage(bytes, Base64ImageConverter.ThumbnailDecodePixels);

        _sourceBytes = thumbnail is null ? null : bytes;
        Image = thumbnail;
        Status = thumbnail is null ? EntryImageStatus.Failed : EntryImageStatus.Ready;
    }

    public BitmapImage? CreateFullSizeImage() =>
        _sourceBytes is null ? null : Base64ImageConverter.ToBitmapImage(_sourceBytes);

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
