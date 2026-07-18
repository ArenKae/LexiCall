// Document racine sauvegardé dans le fichier JSON local.
// Regrouper Entries et Categories dans un seul objet facilite l'évolution du
// format sans multiplier les fichiers de données en Phase 1.
namespace LexiCall.Desktop.Models;

public sealed class VocabularyDatabase
{
    public List<VocabularyEntry> Entries { get; init; } = [];

    public List<VocabularyCategory> Categories { get; init; } = [];
}
