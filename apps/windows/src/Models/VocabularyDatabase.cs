// Root document persisted to the local JSON file.
namespace LexiCall.Desktop.Models;

public sealed class VocabularyDatabase
{
    public List<VocabularyEntry> Entries { get; init; } = [];

    public List<VocabularyCategory> Categories { get; init; } = [];

    // Deletions pushed to the API with no confirmation received yet (see
    // PendingDeletion) — a file predating this field deserializes to an empty
    // list via the default, no migration needed.
    public List<PendingDeletion> PendingEntryDeletions { get; init; } = [];

    public List<PendingDeletion> PendingCategoryDeletions { get; init; } = [];
}
