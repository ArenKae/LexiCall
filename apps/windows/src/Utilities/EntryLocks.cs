// Entry-wide view of VocabularyEntry.LockedFields: the fields the entry
// editor can exclude from AI enrichment, and the all-locked test behind the
// "Verrouillées" filter and the entry list's lock toggle.
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

public static class EntryLocks
{
    public static readonly string[] LockableFields =
        ["Type", "Definition", "Synonyms", "ExampleSentences"];

    public static bool IsFullyLocked(VocabularyEntry entry) =>
        LockableFields.All(entry.LockedFields.Contains);
}
