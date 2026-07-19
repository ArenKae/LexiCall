// Modèle du fichier settings.json unique, partagé par ThemeService (thème) et
// WindowLayoutService (taille/position de la fenêtre, largeur des colonnes).
namespace LexiCall.Desktop.Services;

internal sealed class AppSettings
{
    public string Theme { get; set; } = nameof(AppTheme.Light);

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    public double? CategoryColumnWidth { get; set; }
    public double? EntryListColumnWidth { get; set; }

    // Synchronisation best-effort vers api/ (voir VocabularyApiClient) : vide
    // par défaut = synchronisation désactivée tant qu'aucun serveur n'est
    // configuré. Stockées en clair, comme le reste de ce fichier.
    public string? ApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }
}
