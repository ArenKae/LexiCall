// A logged sync operation (push/pull/delete) for one entry or category —
// see Services/SyncHistoryStore.cs and MainWindowViewModel.RecordSyncHistory.
namespace LexiCall.Desktop.Models;

public enum SyncHistoryEntityType
{
    Entry,
    Category
}

public enum SyncHistoryOperation
{
    Push,
    Pull,
    Delete
}

public enum SyncHistoryOutcome
{
    Success,
    Failure
}

public sealed class SyncHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required DateTimeOffset Timestamp { get; init; }

    public required SyncHistoryEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    // Captured at event time, never looked up later — the entity may since
    // have been renamed or deleted.
    public required string EntityLabel { get; init; }

    public required SyncHistoryOperation Operation { get; init; }

    public required SyncHistoryOutcome Outcome { get; init; }
}
