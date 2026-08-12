// A deletion attempted but not yet confirmed by the API (see
// MainWindowViewModel.DeleteEntry/DeleteCategory/ResyncWithApiAsync) — retried
// with its original DeletedAt until the push succeeds.
namespace LexiCall.Desktop.Models;

public sealed class PendingDeletion
{
    public required Guid Id { get; init; }

    public DateTimeOffset DeletedAt { get; init; }

    // Word/category name at deletion time, for sync-history display. Not
    // required: older persisted queue entries predate this field and must
    // still deserialize (falls back to a truncated Id when empty).
    public string Label { get; init; } = string.Empty;
}
