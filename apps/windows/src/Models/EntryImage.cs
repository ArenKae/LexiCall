// One image attached to a VocabularyEntry (up to 4 per entry).
namespace LexiCall.Desktop.Models;

public sealed class EntryImage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Caption { get; set; } = string.Empty;

    // JPEG re-encoded to base64 (resized/compressed on upload), and filled
    // only for images added on this machine: a pull carries Id/Caption metadata
    // alone, so an entry from another client keeps this empty for good — its
    // bytes are downloaded on display into EntryImageCache instead.
    public string ImageBase64 { get; set; } = string.Empty;
}
