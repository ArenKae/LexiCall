// Représente un mot enregistré dans LexiCall.
// Une entrée peut exister sans catégorie : les catégories servent seulement au tri
// et à l'organisation visuelle, elles ne sont pas obligatoires.
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

    // Image encodée en JPEG puis en base64 (déjà redimensionnée/compressée à
    // l'upload) ; chaîne vide si l'entrée n'a pas d'image.
    public string ImageBase64 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    // Tombstone reçu d'un pull API (voir VocabularyApiClient.TryPullEntriesAsync) :
    // jamais vrai localement en dehors de la fusion, qui supprime aussitôt
    // l'entrée plutôt que de conserver ce champ à true dans Entries.
    public bool IsDeleted { get; init; }

    // Valeur d'UpdatedAt au moment du dernier push confirmé par l'API pour
    // cette entrée précise (voir MainWindowViewModel.ResyncWithApiAsync).
    // Null tant que jamais synchronisée. Pure métadonnée locale, jamais lue
    // ni écrite par l'API.
    public DateTimeOffset? SyncedAt { get; set; }
}
