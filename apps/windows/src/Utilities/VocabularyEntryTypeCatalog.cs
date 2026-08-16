// Enum-value-to-French-label lookup for VocabularyEntryType, shared by the
// entry editor's ComboBox and the detail column's display converter.
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

// A record (not a plain ValueTuple): DisplayMemberPath/SelectedValuePath bind
// via reflection over real properties, which named ValueTuple elements
// aren't — those names are a compile-time-only convenience, so WPF only ever
// sees Item1/Item2 and every ComboBox row renders blank.
public sealed record VocabularyEntryTypeOption(VocabularyEntryType Value, string Label);

public static class VocabularyEntryTypeCatalog
{
    public static IReadOnlyList<VocabularyEntryTypeOption> All { get; } =
    [
        new(VocabularyEntryType.Undefined, "Non défini"),
        new(VocabularyEntryType.NomMasculin, "Nom masculin"),
        new(VocabularyEntryType.NomFeminin, "Nom féminin"),
        new(VocabularyEntryType.Verbe, "Verbe"),
        new(VocabularyEntryType.Adjectif, "Adjectif"),
        new(VocabularyEntryType.Adverbe, "Adverbe"),
        new(VocabularyEntryType.Expression, "Expression")
    ];

    public static string GetLabel(VocabularyEntryType type) =>
        All.FirstOrDefault(item => item.Value == type)?.Label ?? type.ToString();
}
