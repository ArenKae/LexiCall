// Grammatical type of a vocabulary entry.
using System.Text.Json.Serialization;

namespace LexiCall.Desktop.Models;

[JsonConverter(typeof(JsonStringEnumConverter<VocabularyEntryType>))]
public enum VocabularyEntryType
{
    // Explicit = 0 so the CLR default (and any missed initializer) resolves
    // to Undefined rather than the first-declared real value.
    [JsonStringEnumMemberName("Undefined")]
    Undefined = 0,

    [JsonStringEnumMemberName("Nom masculin")]
    NomMasculin,

    [JsonStringEnumMemberName("Nom féminin")]
    NomFeminin,

    [JsonStringEnumMemberName("Verbe")]
    Verbe,

    [JsonStringEnumMemberName("Adjectif")]
    Adjectif,

    [JsonStringEnumMemberName("Adverbe")]
    Adverbe,

    [JsonStringEnumMemberName("Expression")]
    Expression
}
