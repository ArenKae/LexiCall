// One image of the selected entry in the Détails card: the decoded bitmap
// plus its load state, since an entry received from a pull carries image
// metadata only and its bytes arrive from the API a moment later.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

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
        Image = null;
        Status = EntryImageStatus.Loading;
    }

    // A null bitmap is a download that failed or bytes WPF couldn't decode:
    // both leave the thumbnail on its retryable failed state.
    public void MarkLoaded(BitmapImage? image)
    {
        Image = image;
        Status = image is null ? EntryImageStatus.Failed : EntryImageStatus.Ready;
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
