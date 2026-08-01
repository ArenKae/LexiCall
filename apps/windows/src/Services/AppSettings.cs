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

    // Checkpoint de PULL uniquement (voir MainWindowViewModel.ResyncWithApiAsync) :
    // la chaîne X-Sync-Timestamp exacte telle que reçue de l'API, jamais
    // reformatée localement — renvoyée telle quelle comme prochain
    // updated_since, pour que le serveur n'ait jamais à parser un format
    // qu'il n'a pas lui-même produit. Null tant qu'aucun pull n'a réussi
    // (l'API renvoie alors la vue complète). Indépendant du suivi des push,
    // qui se fait par enregistrement via VocabularyEntry/Category.SyncedAt.
    public string? LastPulledAt { get; set; }

    // Couleurs de catégorie choisies manuellement (Id → "#RRGGBB"), voir
    // CategoryColorStore. Purement une préférence de présentation locale à
    // cette installation de l'app Windows : jamais écrite dans vocabulary.json
    // ni synchronisée vers api/, contrairement au reste du modèle de catégorie.
    public Dictionary<Guid, string> CategoryColors { get; set; } = new();

    // Ordre d'affichage choisi manuellement (Id → rang, 0-based au sein d'un
    // même groupe de frères), voir CategoryOrderStore. Même statut que
    // CategoryColors : préférence locale, jamais dans vocabulary.json ni api/.
    public Dictionary<Guid, int> CategoryOrder { get; set; } = new();
}
