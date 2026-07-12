namespace LexiCall.Desktop.Models;

public sealed class VocabularyDatabase
{
    public List<VocabularyEntry> Entries { get; init; } = [];

    public List<VocabularyCategory> Categories { get; init; } = [];
}
