// A vocabulary entry: a word or expression with its definition and metadata.
// Categories are optional — CategoryIds may be empty.
namespace LexiCall.Desktop.Models;

public sealed class VocabularyEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Word { get; set; }

    public required string Definition { get; set; }

    public List<string> Synonyms { get; init; } = [];

    public List<string> ExampleSentences { get; init; } = [];

    public string Notes { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public List<Guid> CategoryIds { get; init; } = [];

    public List<string> Tags { get; init; } = [];

    public VocabularyEntryType Type { get; set; } = VocabularyEntryType.Undefined;

    public bool IsArchived { get; set; }

    // Up to 3 images, enforced at the picker level (EntryEditorWindowViewModel).
    public List<EntryImage> Images { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    // Tombstone flag from an API pull (see VocabularyApiClient.TryPullEntriesAsync).
    // Never stays true locally outside the merge, which removes the entry instead.
    public bool IsDeleted { get; init; }

    // UpdatedAt as of the last confirmed push for this entry (see
    // MainWindowViewModel.ResyncWithApiAsync). Null until ever synced.
    public DateTimeOffset? SyncedAt { get; set; }
}
