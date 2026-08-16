// One image attached to a VocabularyEntry (up to 3 per entry).
namespace LexiCall.Desktop.Models;

public sealed class EntryImage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Caption { get; set; } = string.Empty;

    // JPEG re-encoded to base64 (already resized/compressed on upload).
    // Empty after a pull merge (the API never returns image bytes, only
    // Id/Caption metadata) — callers must tolerate this without crashing.
    public string ImageBase64 { get; set; } = string.Empty;
}
