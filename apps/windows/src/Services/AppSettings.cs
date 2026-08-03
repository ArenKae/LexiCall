// Model for the single settings.json file, shared by ThemeService (theme) and
// WindowLayoutService (window size/position, column widths).
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

    // Sidebar collapse toggle (see MainWindow's CategoryPanelToggleButton) —
    // CategoryColumnWidth above always stores the last *expanded* width, so
    // reopening collapsed still remembers what to restore to on expand.
    public bool CategoryPanelCollapsed { get; set; }

    // Best-effort sync to api/ (see VocabularyApiClient) — empty by default,
    // meaning sync stays disabled until a server is configured.
    public string? ApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }

    // Pull-only checkpoint (see MainWindowViewModel.ResyncWithApiAsync): the
    // X-Sync-Timestamp string as received from the API, never reformatted
    // locally, so the server never has to parse a format it didn't produce
    // itself. Null until a pull has ever succeeded (the API then returns the
    // full live view). Independent of push tracking, which is per-record via
    // VocabularyEntry/Category.SyncedAt.
    public string? LastPulledAt { get; set; }

    // Manually assigned category colors (Id → "#RRGGBB"), see
    // CategoryColorStore. Purely a local presentation preference for this
    // Windows install — never written to vocabulary.json or synced to api/,
    // unlike the rest of the category model.
    public Dictionary<Guid, string> CategoryColors { get; set; } = new();

    // Manually assigned display order (Id → 0-based rank among siblings), see
    // CategoryOrderStore. Same status as CategoryColors: local preference,
    // never in vocabulary.json or api/.
    public Dictionary<Guid, int> CategoryOrder { get; set; } = new();
}
