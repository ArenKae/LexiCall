// A vocabulary entry: a word or expression with its definition and metadata.
// Categories are optional — CategoryIds may be empty. Property declaration
// order here is also the JSON field order written to vocabulary.json
// (System.Text.Json serializes in declaration order) — kept deliberately
// aligned with the equivalent field order normalized into Mongo entries
// documents by api/src/lexicall_api/migration/normalize_entry_fields.py.
namespace LexiCall.Desktop.Models;

public sealed class VocabularyEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Word { get; set; }

    public VocabularyEntryType Type { get; set; } = VocabularyEntryType.Undefined;

    public required string Definition { get; set; }

    public List<Guid> CategoryIds { get; init; } = [];

    public List<string> Synonyms { get; init; } = [];

    public List<string> ExampleSentences { get; init; } = [];

    public string Notes { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    // Up to 3 images, enforced at the picker level (EntryEditorWindowViewModel).
    public List<EntryImage> Images { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public bool IsArchived { get; set; }

    // Tombstone flag from an API pull (see VocabularyApiClient.TryPullEntriesAsync).
    // Never stays true locally outside the merge, which removes the entry instead.
    public bool IsDeleted { get; init; }

    // UpdatedAt as of the last confirmed push for this entry (see
    // MainWindowViewModel.ResyncWithApiAsync). Null until ever synced.
    public DateTimeOffset? SyncedAt { get; set; }
}
