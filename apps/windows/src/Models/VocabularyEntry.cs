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

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
