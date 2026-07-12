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

    public List<string> Categories { get; init; } = [];

    public List<string> Tags { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
