// Enum-value-to-French-label lookup for VocabularyEntryType, shared by the
// entry editor's ComboBox and the detail column's display converter.
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

public static class VocabularyEntryTypeCatalog
{
    public static IReadOnlyList<(VocabularyEntryType Value, string Label)> All { get; } =
    [
        (VocabularyEntryType.Undefined, "Non défini"),
        (VocabularyEntryType.NomMasculin, "Nom masculin"),
        (VocabularyEntryType.NomFeminin, "Nom féminin"),
        (VocabularyEntryType.Verbe, "Verbe"),
        (VocabularyEntryType.Adjectif, "Adjectif"),
        (VocabularyEntryType.Adverbe, "Adverbe"),
        (VocabularyEntryType.Expression, "Expression")
    ];

    public static string GetLabel(VocabularyEntryType type) =>
        All.FirstOrDefault(item => item.Value == type).Label ?? type.ToString();
}
