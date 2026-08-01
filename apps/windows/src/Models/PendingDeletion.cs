// Trace une suppression tentée mais pas encore confirmée par l'API (voir
// MainWindowViewModel.DeleteEntry/DeleteCategory/ResyncWithApiAsync) —
// rejouée tant que le push échoue, avec le DeletedAt d'origine.
namespace LexiCall.Desktop.Models;

public sealed class PendingDeletion
{
    public required Guid Id { get; init; }

    public DateTimeOffset DeletedAt { get; init; }
}
