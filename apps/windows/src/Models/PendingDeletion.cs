// A deletion attempted but not yet confirmed by the API (see
// MainWindowViewModel.DeleteEntry/DeleteCategory/ResyncWithApiAsync) — retried
// with its original DeletedAt until the push succeeds.
namespace LexiCall.Desktop.Models;

public sealed class PendingDeletion
{
    public required Guid Id { get; init; }

    public DateTimeOffset DeletedAt { get; init; }
}
