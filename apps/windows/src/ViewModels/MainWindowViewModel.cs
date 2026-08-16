// Main ViewModel: exposes the category tree, the filtered entry list
// (category + search), and CRUD operations on entries and categories to
// MainWindow.xaml.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

// Global status shown next to the Options button (see MainWindow.xaml) —
// combines connectivity (ApiConnectionStatus) and the last resync outcome
// into one XAML-friendly state. Problem covers both an unreachable API and
// a pull that fails despite a healthy connectivity check; the cause isn't
// distinguished further here.
public enum GlobalSyncStatus
{
    NotConfigured,
    Syncing,
    Ok,
    Problem
}

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyRepository _repository;
    private VocabularyApiClient _apiClient;
    private string? _apiBaseUrl;
    private string? _apiKey;
    private string _searchQuery = string.Empty;
    private string _searchStatusText = string.Empty;
    private VocabularyEntry? _selectedEntry;
    private CategoryNodeViewModel? _selectedCategoryNode;
    private HashSet<Guid>? _activeCategoryFilterIds;
    private readonly List<PendingDeletion> _pendingEntryDeletions;
    private readonly List<PendingDeletion> _pendingCategoryDeletions;
    private readonly DispatcherTimer _periodicSyncTimer;
    private bool _isSyncing;
    private GlobalSyncStatus _globalSyncStatus = GlobalSyncStatus.NotConfigured;
    private DateTimeOffset? _lastSyncedAt;

    public MainWindowViewModel(VocabularyRepository? repository = null, VocabularyApiClient? apiClient = null)
    {
        _repository = repository ?? new VocabularyRepository();

        var database = _repository.DataFileExists
            ? _repository.LoadDatabase()
            : CreateSampleDatabase();

        Entries = new ObservableCollection<VocabularyEntry>(database.Entries);
        Categories = new ObservableCollection<VocabularyCategory>(database.Categories);
        _pendingEntryDeletions = database.PendingEntryDeletions;
        _pendingCategoryDeletions = database.PendingCategoryDeletions;
        SyncHistory = new ObservableCollection<SyncHistoryEntry>(SyncHistoryStore.Load());
        FilteredEntries = [];
        CategoryTree = [];
        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.FirstOrDefault();

        if (!_repository.DataFileExists)
        {
            SaveDatabase();
        }

        var settings = SettingsStore.Load();
        _apiBaseUrl = settings.ApiBaseUrl;
        _apiKey = settings.ApiKey;
        _apiClient = apiClient ?? new VocabularyApiClient(_apiBaseUrl, _apiKey);

        // Periodic sync while the app runs (not just at launch): a network
        // outage that resolves mid-session (VPN reconnects, dev VM restarts)
        // is caught without waiting for the next app launch.
        _periodicSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _periodicSyncTimer.Tick += async (_, _) => await TryResyncAsync();
        _periodicSyncTimer.Start();

        // First catch-up attempt for offline mutations never yet pushed to
        // the API — see ResyncWithApiAsync. Deliberately not awaited: must
        // never delay the window opening.
        _ = TryResyncAsync();
    }

    public ObservableCollection<VocabularyEntry> Entries { get; }

    public ObservableCollection<VocabularyCategory> Categories { get; }

    // Newest-first log of push/pull/delete operations, for SyncHistoryWindow.
    // See RecordSyncHistory.
    public ObservableCollection<SyncHistoryEntry> SyncHistory { get; }

    // List shown by the UI, rebuilt from Entries on every search or mutation.
    public ObservableCollection<VocabularyEntry> FilteredEntries { get; }

    // Sidebar tree: virtual nodes ("Toutes les entrées", "Sans catégorie")
    // followed by root categories with their subcategories.
    public ObservableCollection<CategoryNodeViewModel> CategoryTree { get; }

    public string DataFilePath => _repository.FilePath;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public string SearchStatusText
    {
        get => _searchStatusText;
        private set => SetProperty(ref _searchStatusText, value);
    }

    public bool HasEntries => Entries.Count > 0;

    public bool HasFilteredEntries => FilteredEntries.Count > 0;

    public bool HasSelectedEntry => SelectedEntry is not null;

    public bool HasCategories => Categories.Count > 0;

    public CategoryNodeViewModel? SelectedCategoryNode => _selectedCategoryNode;

    // Leaf name only; the full path is exposed via SelectedCategoryTooltip.
    public string SelectedCategoryLabel => _selectedCategoryNode?.DisplayName ?? "Toutes les entrées";

    // Null when the full path wouldn't add anything (virtual node or root category).
    public string? SelectedCategoryTooltip => _selectedCategoryNode?.Category is VocabularyCategory { ParentId: not null } category
        ? GetCategoryPath(category)
        : null;

    // Full objects, not just names: detail chips need the Id for their click
    // handler (see SelectCategory).
    public IReadOnlyList<VocabularyCategory> SelectedEntryCategories => SelectedEntry is null
        ? []
        : GetCategories(SelectedEntry.CategoryIds).ToList();

    // Bottom-left status bar (MainWindow.xaml): empty when there's nothing to
    // show (no selection, or sync not configured) — the whole bar hides
    // instead of showing a misleading state to someone not using sync.
    public string SelectedEntrySyncStatusText
    {
        get
        {
            if (SelectedEntry is not { } entry || !_apiClient.IsConfigured)
            {
                return string.Empty;
            }

            return entry.SyncedAt switch
            {
                null => "Jamais synchronisé",
                { } syncedAt when syncedAt < entry.UpdatedAt => "Synchronisation en attente",
                { } syncedAt => $"Synchronisé le {syncedAt.LocalDateTime:g}"
            };
        }
    }

    // Drives the status dot's color in MainWindow.xaml.
    public bool SelectedEntrySyncIsSynced =>
        SelectedEntry is { SyncedAt: { } syncedAt } entry && syncedAt >= entry.UpdatedAt;

    // Global status shown next to the Options button — see GlobalSyncStatus
    // and ResyncWithApiAsync for where it's set.
    public GlobalSyncStatus GlobalSyncStatus
    {
        get => _globalSyncStatus;
        private set => SetProperty(ref _globalSyncStatus, value);
    }

    // Local time of the last successful resync cycle — shown in the
    // footer's sync status tooltip.
    public DateTimeOffset? LastSyncedAt
    {
        get => _lastSyncedAt;
        private set => SetProperty(ref _lastSyncedAt, value);
    }

    public string LastSyncedAtTooltip => LastSyncedAt is { } lastSyncedAt
        ? $"Dernière synchronisation le : {lastSyncedAt:dd/MM/yyyy à HH:mm:ss}"
        : "Pas encore synchronisé";

    public string EmptyListMessage
    {
        get
        {
            if (!HasEntries)
            {
                return "Aucun mot pour l’instant. Clique sur « Ajouter un mot » pour commencer.";
            }

            return string.IsNullOrWhiteSpace(SearchQuery)
                ? "Aucun mot dans cette catégorie."
                : "Aucun résultat pour cette recherche.";
        }
    }

    public string EmptyDetailMessage
    {
        get
        {
            if (!HasEntries)
            {
                return "Ajoute ton premier mot pour commencer à construire ton vocabulaire.";
            }

            return HasFilteredEntries
                ? "Sélectionne un mot dans la liste pour afficher ses détails."
                : "Aucun mot ne correspond à ce filtre.";
        }
    }

    public VocabularyEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                OnPropertyChanged(nameof(HasSelectedEntry));
                OnPropertyChanged(nameof(SelectedEntryCategories));
                OnPropertyChanged(nameof(EmptyDetailMessage));
                OnPropertyChanged(nameof(SelectedEntrySyncStatusText));
                OnPropertyChanged(nameof(SelectedEntrySyncIsSynced));
                OnPropertyChanged(nameof(ArchiveButtonText));
            }
        }
    }

    public string ArchiveButtonText => SelectedEntry?.IsArchived == true ? "Désarchiver" : "Archiver";

    public string ThemeToggleText => ThemeService.CurrentTheme == AppTheme.Dark
        ? "☀  Thème clair"
        : "🌙  Thème sombre";

    public string ApiBaseUrl => _apiBaseUrl ?? string.Empty;

    public string ApiKey => _apiKey ?? string.Empty;

    // Toggled by MainWindow.xaml.cs around every ShowDialog() on
    // EntryEditorWindow/CategoryEditorWindow — stops TryResyncAsync from
    // merging a pull while an edit is in progress. A plain bool rather than
    // an Application.Current.Windows check: ViewModels never reference a
    // concrete Window type.
    public bool IsEditorDialogOpen { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleTheme()
    {
        ThemeService.Toggle();
        OnPropertyChanged(nameof(ThemeToggleText));
    }

    // Called by OptionsWindow: persists (load-merge-save, like the rest of
    // settings.json) then rebuilds the HTTP client with the new values.
    public void UpdateApiSettings(string? apiBaseUrl, string? apiKey)
    {
        _apiBaseUrl = apiBaseUrl;
        _apiKey = apiKey;

        var settings = SettingsStore.Load();
        settings.ApiBaseUrl = apiBaseUrl;
        settings.ApiKey = apiKey;
        SettingsStore.Save(settings);

        _apiClient = new VocabularyApiClient(apiBaseUrl, apiKey);
        OnPropertyChanged(nameof(ApiBaseUrl));
        OnPropertyChanged(nameof(ApiKey));

        // Both sync indicators depend on _apiClient.IsConfigured (hidden when
        // false) — without this they'd stay stuck on the old state until
        // the next resync cycle (up to 60s later).
        GlobalSyncStatus = _apiClient.IsConfigured ? GlobalSyncStatus.Syncing : GlobalSyncStatus.NotConfigured;
        OnPropertyChanged(nameof(SelectedEntrySyncStatusText));
        OnPropertyChanged(nameof(SelectedEntrySyncIsSynced));
    }

    // Called from OptionsWindow's "Tester la connexion" button — reflects the
    // manual test result on GlobalSyncStatus immediately, rather than leaving
    // the sidebar footer stuck on its previous state until the next periodic
    // resync (up to 60s later).
    public async Task<ApiConnectionStatus> TestApiConnectionAsync()
    {
        // No ConfigureAwait(false): the only caller is a UI-thread async void
        // handler (OptionsWindow), and GlobalSyncStatus must be set back on
        // that thread for the bound footer to update safely.
        var status = await _apiClient.TestConnectionAsync();

        GlobalSyncStatus = status switch
        {
            ApiConnectionStatus.Ok => GlobalSyncStatus.Ok,
            ApiConnectionStatus.NotConfigured => GlobalSyncStatus.NotConfigured,
            _ => GlobalSyncStatus.Problem
        };

        return status;
    }

    // Shared entry point for startup and the periodic timer — re-entrancy
    // guard (a resync already running skips a second one) and edit guard
    // (see IsEditorDialogOpen). Both checked at trigger time: DispatcherTimer
    // .Tick and IsEditorDialogOpen writes both run on the UI thread, so
    // there's no race to handle. If either guard trips, this call is simply
    // skipped — the next tick, 60 seconds later, retries on its own.
    private async Task TryResyncAsync()
    {
        if (_isSyncing || IsEditorDialogOpen)
        {
            return;
        }

        _isSyncing = true;

        try
        {
            await ResyncWithApiAsync().ConfigureAwait(false);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    // Catches up on mutations made while the API was unreachable (every
    // mutation already attempts a best-effort push when it happens, see
    // AddEntry/UpdateEntry/SaveCategory/RenameCategory; this covers failures
    // of those attempts) and pulls changes made by another client.
    // Categories are pushed/pulled before entries: the API validates an
    // entry's CategoryIds against categories it already knows about.
    private async Task ResyncWithApiAsync()
    {
        if (!_apiClient.IsConfigured)
        {
            GlobalSyncStatus = GlobalSyncStatus.NotConfigured;
            return;
        }

        // Still on the caller's thread here (no await yet) — no
        // Dispatcher.Invoke needed for these two writes.
        GlobalSyncStatus = GlobalSyncStatus.Syncing;
        var status = await _apiClient.TestConnectionAsync().ConfigureAwait(false);

        if (status != ApiConnectionStatus.Ok)
        {
            // Unreachable or misconfigured API: no point attempting hundreds
            // of upserts that would all fail, on every startup.
            Application.Current.Dispatcher.Invoke(() => GlobalSyncStatus = GlobalSyncStatus.Problem);
            return;
        }

        // Retries pending deletions. Entries before categories — the
        // reverse of the "categories before entries" convention below: the
        // server's count_entries_using_category guard still counts an entry
        // until it's tombstoned, so a pending category deletion would 409
        // if the entry that used it isn't already gone server-side.
        var entryDeletionResults = new List<(PendingDeletion Pending, bool Success)>();

        foreach (var pending in _pendingEntryDeletions.ToList())
        {
            var success = await _apiClient.TryDeleteEntryAsync(pending.Id, pending.DeletedAt).ConfigureAwait(false);
            entryDeletionResults.Add((pending, success));

            if (success)
            {
                _pendingEntryDeletions.Remove(pending);
            }
        }

        var categoryDeletionResults = new List<(PendingDeletion Pending, bool Success)>();

        foreach (var pending in _pendingCategoryDeletions.ToList())
        {
            var success = await _apiClient.TryDeleteCategoryAsync(pending.Id, pending.DeletedAt).ConfigureAwait(false);
            categoryDeletionResults.Add((pending, success));

            if (success)
            {
                _pendingCategoryDeletions.Remove(pending);
            }
        }

        // Push gated per record via SyncedAt, not a global checkpoint:
        // isolates a permanent failure to one record instead of blocking the
        // whole batch, and naturally covers the very first sync (SyncedAt
        // == null for everyone).
        var categoriesToPush = Categories.Where(c => c.SyncedAt is null || c.SyncedAt < c.UpdatedAt).ToList();
        var syncedCategories = new List<VocabularyCategory>();
        var categoryPushResults = new List<(VocabularyCategory Category, bool Success)>();

        foreach (var category in categoriesToPush)
        {
            var success = await _apiClient.TryUpsertCategoryAsync(category).ConfigureAwait(false);
            categoryPushResults.Add((category, success));

            if (success)
            {
                syncedCategories.Add(category);
            }
        }

        var entriesToPush = Entries.Where(e => e.SyncedAt is null || e.SyncedAt < e.UpdatedAt).ToList();
        var syncedEntries = new List<VocabularyEntry>();
        var entryPushResults = new List<(VocabularyEntry Entry, bool Success)>();

        foreach (var entry in entriesToPush)
        {
            var success = await _apiClient.TryUpsertEntryAsync(entry).ConfigureAwait(false);
            entryPushResults.Add((entry, success));

            if (success)
            {
                syncedEntries.Add(entry);
            }
        }

        // Delta pull: checkpoint is the last successful pull (LastPulledAt),
        // entirely independent from whether the pushes above succeeded —
        // each record already tracks its own state via SyncedAt. Null means
        // the very first pull, already handled server-side (full view, no
        // updated_since).
        var checkpoint = SettingsStore.Load().LastPulledAt;
        var categoriesPull = await _apiClient.TryPullCategoriesAsync(checkpoint).ConfigureAwait(false);

        if (categoriesPull is null)
        {
            // Failure: checkpoint and SyncedAt stay unchanged, retried on
            // the next trigger (next tick or launch).
            Application.Current.Dispatcher.Invoke(() => GlobalSyncStatus = GlobalSyncStatus.Problem);
            return;
        }

        var entriesPull = await _apiClient.TryPullEntriesAsync(checkpoint).ConfigureAwait(false);

        if (entriesPull is null)
        {
            Application.Current.Dispatcher.Invoke(() => GlobalSyncStatus = GlobalSyncStatus.Problem);
            return;
        }

        // One UI-thread dispatch for the whole batch (push confirmations and
        // pull merge) instead of one Dispatcher.Invoke per record —
        // Entries/Categories are UI-bound ObservableCollections; nothing
        // before this point writes to them from this background thread
        // (only reads via .ToList()/.Where()).
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var category in syncedCategories)
            {
                category.SyncedAt = category.UpdatedAt;
            }

            foreach (var entry in syncedEntries)
            {
                entry.SyncedAt = entry.UpdatedAt;
            }

            foreach (var (pending, success) in entryDeletionResults)
            {
                RecordSyncHistory(SyncHistoryEntityType.Entry, pending.Id,
                    string.IsNullOrEmpty(pending.Label) ? pending.Id.ToString()[..8] : pending.Label,
                    SyncHistoryOperation.Delete, success ? SyncHistoryOutcome.Success : SyncHistoryOutcome.Failure);
            }

            foreach (var (pending, success) in categoryDeletionResults)
            {
                RecordSyncHistory(SyncHistoryEntityType.Category, pending.Id,
                    string.IsNullOrEmpty(pending.Label) ? pending.Id.ToString()[..8] : pending.Label,
                    SyncHistoryOperation.Delete, success ? SyncHistoryOutcome.Success : SyncHistoryOutcome.Failure);
            }

            foreach (var (category, success) in categoryPushResults)
            {
                RecordSyncHistory(SyncHistoryEntityType.Category, category.Id, category.Name,
                    SyncHistoryOperation.Push, success ? SyncHistoryOutcome.Success : SyncHistoryOutcome.Failure,
                    GetChangeKind(category.CreatedAt, category.UpdatedAt));
            }

            foreach (var (entry, success) in entryPushResults)
            {
                RecordSyncHistory(SyncHistoryEntityType.Entry, entry.Id, entry.Word,
                    SyncHistoryOperation.Push, success ? SyncHistoryOutcome.Success : SyncHistoryOutcome.Failure,
                    GetChangeKind(entry.CreatedAt, entry.UpdatedAt));
            }

            MergePulled(Categories, categoriesPull.Items, FindCategoryIndex, c => c.Id, c => c.UpdatedAt, c => c.CreatedAt, c => c.IsDeleted, (c, t) => c.SyncedAt = t, SyncHistoryEntityType.Category, c => c.Name);
            MergePulled(Entries, entriesPull.Items, FindEntryIndex, e => e.Id, e => e.UpdatedAt, e => e.CreatedAt, e => e.IsDeleted, (e, t) => e.SyncedAt = t, SyncHistoryEntityType.Entry, e => e.Word);
            RebuildCategoryTree();
            RefreshFilteredEntries();
            SaveDatabase();
            GlobalSyncStatus = GlobalSyncStatus.Ok;
            LastSyncedAt = DateTimeOffset.Now;
            OnPropertyChanged(nameof(LastSyncedAtTooltip));

            // Unconditional rather than scoped to the selected entry: cheap
            // (one cycle per minute), and covers both a batched push
            // confirmation (in-place mutation, as in PushEntryUpsertAsync)
            // and a delta-pull update — the "live" trigger the status bar
            // needs.
            OnPropertyChanged(nameof(SelectedEntrySyncStatusText));
            OnPropertyChanged(nameof(SelectedEntrySyncIsSynced));
        });

        if (categoriesPull.ServerTimestamp is { } newCheckpoint)
        {
            var latest = SettingsStore.Load();
            latest.LastPulledAt = newCheckpoint;
            SettingsStore.Save(latest);
        }
    }

    // Merges records from a delta pull into a local ObservableCollection:
    // removes if tombstoned, otherwise inserts or replaces (only if actually
    // newer — a defensive check, the server already returns only rows newer
    // than the checkpoint). Generic over VocabularyEntry and
    // VocabularyCategory via small delegate accessors, since the two models
    // share no common interface.
    private void MergePulled<T>(
        ObservableCollection<T> collection,
        IReadOnlyList<T> pulled,
        Func<Guid, int> findIndex,
        Func<T, Guid> getId,
        Func<T, DateTimeOffset> getUpdatedAt,
        Func<T, DateTimeOffset> getCreatedAt,
        Func<T, bool> getIsDeleted,
        Action<T, DateTimeOffset> setSyncedAt,
        SyncHistoryEntityType entityType,
        Func<T, string> getLabel)
    {
        foreach (var item in pulled)
        {
            var index = findIndex(getId(item));

            if (getIsDeleted(item))
            {
                if (index >= 0)
                {
                    // The transport is a pull, but what actually happened to
                    // this record is a deletion — logged as Delete so the
                    // history reads by effect, not by mechanism.
                    RecordSyncHistory(entityType, getId(item), getLabel(collection[index]), SyncHistoryOperation.Delete, SyncHistoryOutcome.Success);
                    collection.RemoveAt(index);
                }

                continue;
            }

            // A record just received from the server is by definition
            // already in sync — without this it would stay marked "needs
            // push" (SyncedAt still null on the deserialized object) and get
            // pushed back for nothing on the next resync, including the
            // entire first pull.
            if (index < 0)
            {
                setSyncedAt(item, getUpdatedAt(item));
                collection.Add(item);
                RecordSyncHistory(entityType, getId(item), getLabel(item), SyncHistoryOperation.Pull, SyncHistoryOutcome.Success,
                    GetChangeKind(getCreatedAt(item), getUpdatedAt(item)));
            }
            else if (getUpdatedAt(item) > getUpdatedAt(collection[index]))
            {
                setSyncedAt(item, getUpdatedAt(item));
                collection[index] = item;
                RecordSyncHistory(entityType, getId(item), getLabel(item), SyncHistoryOperation.Pull, SyncHistoryOutcome.Success,
                    GetChangeKind(getCreatedAt(item), getUpdatedAt(item)));
            }
        }
    }

    // Must only be called on the UI thread: mutates the UI-bound SyncHistory
    // collection directly.
    private void RecordSyncHistory(SyncHistoryEntityType entityType, Guid entityId, string entityLabel,
        SyncHistoryOperation operation, SyncHistoryOutcome outcome, SyncHistoryChangeKind? changeKind = null)
    {
        SyncHistory.Insert(0, new SyncHistoryEntry
        {
            Timestamp = DateTimeOffset.Now,
            EntityType = entityType,
            EntityId = entityId,
            EntityLabel = entityLabel,
            Operation = operation,
            Outcome = outcome,
            ChangeKind = changeKind
        });

        while (SyncHistory.Count > SyncHistoryStore.MaxEntries)
        {
            SyncHistory.RemoveAt(SyncHistory.Count - 1);
        }

        SyncHistoryStore.Save(SyncHistory.ToList());
    }

    // Push/pull history rows show whether the underlying data was created or
    // edited — irrelevant for Delete, which already says so via Operation.
    // CreatedAt == UpdatedAt exactly at creation time (both editors stamp them
    // from the same DateTimeOffset.Now call), so any difference means at
    // least one edit happened since.
    private static SyncHistoryChangeKind GetChangeKind(DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        createdAt == updatedAt ? SyncHistoryChangeKind.Created : SyncHistoryChangeKind.Updated;

    // Called from SyncHistoryWindow after user confirmation.
    public void ClearSyncHistory()
    {
        SyncHistory.Clear();
        SyncHistoryStore.Save(SyncHistory.ToList());
    }

    // Immediate push per mutation: confirms SyncedAt on success rather than
    // fire-and-forget, so ResyncWithApiAsync's gated push knows this record
    // no longer needs pushing.
    private async Task PushEntryUpsertAsync(VocabularyEntry entry)
    {
        if (!await _apiClient.TryUpsertEntryAsync(entry).ConfigureAwait(false))
        {
            Application.Current.Dispatcher.Invoke(() =>
                RecordSyncHistory(SyncHistoryEntityType.Entry, entry.Id, entry.Word, SyncHistoryOperation.Push, SyncHistoryOutcome.Failure,
                    GetChangeKind(entry.CreatedAt, entry.UpdatedAt)));
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            var index = FindEntryIndex(entry.Id);

            // Only marks synced if the entry wasn't edited again in the
            // meantime (a new edit before the previous one's confirmation)
            // — otherwise a newer version would be wrongly marked as synced.
            if (index >= 0 && Entries[index].UpdatedAt == entry.UpdatedAt)
            {
                Entries[index].SyncedAt = entry.UpdatedAt;
                SaveDatabase();
                RecordSyncHistory(SyncHistoryEntityType.Entry, entry.Id, entry.Word, SyncHistoryOperation.Push, SyncHistoryOutcome.Success,
                    GetChangeKind(entry.CreatedAt, entry.UpdatedAt));

                // In-place mutation: SelectedEntry isn't reassigned on this
                // path (unlike MergePulled), so its setter doesn't notify on
                // its own — needed for the status bar badge to update
                // without re-selecting the entry.
                if (SelectedEntry?.Id == entry.Id)
                {
                    OnPropertyChanged(nameof(SelectedEntrySyncStatusText));
                    OnPropertyChanged(nameof(SelectedEntrySyncIsSynced));
                }
            }
        });
    }

    private async Task PushCategoryUpsertAsync(VocabularyCategory category)
    {
        if (!await _apiClient.TryUpsertCategoryAsync(category).ConfigureAwait(false))
        {
            Application.Current.Dispatcher.Invoke(() =>
                RecordSyncHistory(SyncHistoryEntityType.Category, category.Id, category.Name, SyncHistoryOperation.Push, SyncHistoryOutcome.Failure,
                    GetChangeKind(category.CreatedAt, category.UpdatedAt)));
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            var index = FindCategoryIndex(category.Id);

            if (index >= 0 && Categories[index].UpdatedAt == category.UpdatedAt)
            {
                Categories[index].SyncedAt = category.UpdatedAt;
                SaveDatabase();
                RecordSyncHistory(SyncHistoryEntityType.Category, category.Id, category.Name, SyncHistoryOperation.Push, SyncHistoryOutcome.Success,
                    GetChangeKind(category.CreatedAt, category.UpdatedAt));
            }
        });
    }

    // Pushes a pending deletion; only clears it from the queue on
    // confirmation. If the push fails (offline), the record stays in
    // _pendingEntryDeletions/_pendingCategoryDeletions (persisted in
    // vocabulary.json), retried on the next ResyncWithApiAsync.
    private async Task PushEntryDeletionAsync(Guid id, string label, DateTimeOffset deletedAt)
    {
        if (!await _apiClient.TryDeleteEntryAsync(id, deletedAt).ConfigureAwait(false))
        {
            Application.Current.Dispatcher.Invoke(() =>
                RecordSyncHistory(SyncHistoryEntityType.Entry, id, label, SyncHistoryOperation.Delete, SyncHistoryOutcome.Failure));
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            _pendingEntryDeletions.RemoveAll(p => p.Id == id);
            SaveDatabase();
            RecordSyncHistory(SyncHistoryEntityType.Entry, id, label, SyncHistoryOperation.Delete, SyncHistoryOutcome.Success);
        });
    }

    private async Task PushCategoryDeletionAsync(Guid id, string label, DateTimeOffset deletedAt)
    {
        if (!await _apiClient.TryDeleteCategoryAsync(id, deletedAt).ConfigureAwait(false))
        {
            Application.Current.Dispatcher.Invoke(() =>
                RecordSyncHistory(SyncHistoryEntityType.Category, id, label, SyncHistoryOperation.Delete, SyncHistoryOutcome.Failure));
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            _pendingCategoryDeletions.RemoveAll(p => p.Id == id);
            SaveDatabase();
            RecordSyncHistory(SyncHistoryEntityType.Category, id, label, SyncHistoryOperation.Delete, SyncHistoryOutcome.Success);
        });
    }

    public void AddEntry(VocabularyEntry entry)
    {
        Entries.Insert(0, entry);
        OnEntriesChanged();

        // Search is reset so the newly added word stays visible; the
        // selected category is preserved by RebuildCategoryTree.
        _searchQuery = string.Empty;
        OnPropertyChanged(nameof(SearchQuery));

        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = entry;
        SaveDatabase();

        // Best-effort, never awaited: must never slow down the UI,
        // especially when the API is unreachable (still the common case for now).
        _ = PushEntryUpsertAsync(entry);
    }

    public void UpdateEntry(VocabularyEntry updatedEntry)
    {
        var index = FindEntryIndex(updatedEntry.Id);

        if (index < 0)
        {
            return;
        }

        Entries[index] = updatedEntry;
        RebuildCategoryTree();
        RefreshFilteredEntries();

        if (FilteredEntries.Any(entry => entry.Id == updatedEntry.Id))
        {
            SelectedEntry = updatedEntry;
        }

        SaveDatabase();
        _ = PushEntryUpsertAsync(updatedEntry);
    }

    // Quick-toggle from the detail column's Archiver/Désarchiver button — the
    // same field is also editable via a checkbox in the entry editor form,
    // both paths go through the same IsArchived property on VocabularyEntry.
    public void ToggleArchiveEntry(VocabularyEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.IsArchived = !entry.IsArchived;
        entry.UpdatedAt = DateTimeOffset.Now;
        // Archiving/unarchiving always flips the entry's visibility in
        // whatever view it was just selected from, so RefreshFilteredEntries
        // always ends up picking a new SelectedEntry — its own setter is
        // what refreshes ArchiveButtonText, no extra notification needed here.
        RebuildCategoryTree();
        RefreshFilteredEntries();
        SaveDatabase();
        _ = PushEntryUpsertAsync(entry);
    }

    public void DeleteEntry(VocabularyEntry entry)
    {
        var index = FindEntryIndex(entry.Id);

        if (index < 0)
        {
            return;
        }

        Entries.RemoveAt(index);
        OnEntriesChanged();
        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.Count == 0
            ? null
            : FilteredEntries[Math.Min(index, FilteredEntries.Count - 1)];

        var deletedAt = DateTimeOffset.Now;
        _pendingEntryDeletions.Add(new PendingDeletion { Id = entry.Id, DeletedAt = deletedAt, Label = entry.Word });
        SaveDatabase();
        _ = PushEntryDeletionAsync(entry.Id, entry.Word, deletedAt);
    }

    // Adds or replaces a category validated by CategoryEditorWindow. Returns
    // an error message, or null on success.
    public string? SaveCategory(VocabularyCategory category)
    {
        // Anti-cycle guard: the editor already excludes descendants from the
        // parent picker, but this re-checks before persisting.
        if (category.ParentId is Guid parentId &&
            (parentId == category.Id ||
             CategoryHierarchy.GetDescendantIds(Categories, category.Id).Contains(parentId)))
        {
            return "Le parent choisi créerait un cycle dans la hiérarchie.";
        }

        var index = FindCategoryIndex(category.Id);

        if (index < 0)
        {
            Categories.Add(category);
        }
        else
        {
            Categories[index] = category;
        }

        OnCategoriesChanged();
        _ = PushCategoryUpsertAsync(category);
        return null;
    }

    // Manually assigned category color (or null to revert to automatic),
    // called after a successful SaveCategory from CategoryEditorWindow. A
    // local Windows-app preference (CategoryColorStore) — touches neither
    // vocabulary.json nor api/.
    public void SetCategoryColor(Guid categoryId, string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex))
        {
            CategoryColorStore.ClearColor(categoryId);
        }
        else
        {
            CategoryColorStore.SetColor(categoryId, colorHex);
        }

        OnCategoriesChanged();
    }

    public string? RenameCategory(Guid categoryId, string newName)
    {
        var index = FindCategoryIndex(categoryId);

        if (index < 0)
        {
            return null;
        }

        var name = newName.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Le nom est obligatoire.";
        }

        var category = Categories[index];

        var duplicateExists = Categories.Any(other =>
            other.Id != categoryId &&
            other.ParentId == category.ParentId &&
            string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            return "Une catégorie porte déjà ce nom au même niveau.";
        }

        category.Name = name;
        category.UpdatedAt = DateTimeOffset.Now;
        OnCategoriesChanged();
        _ = PushCategoryUpsertAsync(category);
        return null;
    }

    public string? DeleteCategory(Guid categoryId)
    {
        var index = FindCategoryIndex(categoryId);

        if (index < 0)
        {
            return null;
        }

        if (Categories.Any(category => category.ParentId == categoryId))
        {
            return "Impossible de supprimer une catégorie qui contient des sous-catégories.";
        }

        var usageCount = Entries.Count(entry => entry.CategoryIds.Contains(categoryId));

        if (usageCount > 0)
        {
            return $"Impossible de supprimer : cette catégorie est utilisée par {usageCount} mot(s).";
        }

        var categoryName = Categories[index].Name;
        Categories.RemoveAt(index);
        CategoryColorStore.ClearColor(categoryId);
        CategoryOrderStore.ClearOrder(categoryId);

        var deletedAt = DateTimeOffset.Now;
        _pendingCategoryDeletions.Add(new PendingDeletion { Id = categoryId, DeletedAt = deletedAt, Label = categoryName });

        // OnCategoriesChanged() already saves — one SaveDatabase() call for
        // both the deletion and the pending-deletion record, not two separate calls.
        OnCategoriesChanged();
        _ = PushCategoryDeletionAsync(categoryId, categoryName, deletedAt);
        return null;
    }

    // Moves a category among its siblings (same effective parent) and
    // persists the resulting order for the whole group in CategoryOrderStore
    // — a local Windows-app preference, touches neither vocabulary.json nor api/.
    public void MoveCategoryUp(Guid categoryId) => MoveCategory(categoryId, offset: -1);

    public void MoveCategoryDown(Guid categoryId) => MoveCategory(categoryId, offset: 1);

    private void MoveCategory(Guid categoryId, int offset)
    {
        var category = Categories.FirstOrDefault(c => c.Id == categoryId);

        if (category is null)
        {
            return;
        }

        var siblings = CategoryHierarchy
            .GetSiblingsInOrder(Categories, category, CategoryOrderStore.LoadAll())
            .ToList();
        var index = siblings.FindIndex(sibling => sibling.Id == categoryId);
        var targetIndex = index + offset;

        if (targetIndex < 0 || targetIndex >= siblings.Count)
        {
            return;
        }

        (siblings[index], siblings[targetIndex]) = (siblings[targetIndex], siblings[index]);
        CategoryOrderStore.SetOrder(siblings.Select(sibling => sibling.Id));
        RebuildCategoryTree();
    }

    private void OnCategoriesChanged()
    {
        RebuildCategoryTree();
        OnPropertyChanged(nameof(SelectedEntryCategories));
        RefreshFilteredEntries();
        SaveDatabase();
    }

    // Called by a node when the TreeView selects it (IsSelected binding).
    private void OnCategoryNodeSelected(CategoryNodeViewModel node)
    {
        if (_selectedCategoryNode == node)
        {
            return;
        }

        // The ViewModel must stay consistent even with no view attached.
        var previousNode = _selectedCategoryNode;
        _selectedCategoryNode = node;
        previousNode?.IsSelected = false;
        OnPropertyChanged(nameof(SelectedCategoryNode));
        OnPropertyChanged(nameof(SelectedCategoryLabel));
        OnPropertyChanged(nameof(SelectedCategoryTooltip));
        RefreshFilteredEntries();
    }

    // Category-chip click: selects the same category in the left-hand tree.
    public void SelectCategory(Guid categoryId)
    {
        var node = CollectNodes(CategoryTree)
            .FirstOrDefault(candidate => candidate.Category?.Id == categoryId);

        if (node is null)
        {
            return;
        }

        foreach (var ancestor in GetAncestors(node))
        {
            ancestor.IsExpanded = true;
        }

        node.IsSelected = true;
    }

    // Walks up parents in the displayed tree (not just the category
    // hierarchy) to expand down to the target node.
    private IEnumerable<CategoryNodeViewModel> GetAncestors(CategoryNodeViewModel node)
    {
        var ancestorIds = new List<Guid>();
        var current = node.Category;
        var categoriesById = Categories.ToDictionary(category => category.Id);

        while (current?.ParentId is Guid parentId && categoriesById.TryGetValue(parentId, out var parent))
        {
            ancestorIds.Add(parent.Id);
            current = parent;
        }

        return CollectNodes(CategoryTree).Where(candidate => candidate.Category is not null && ancestorIds.Contains(candidate.Category.Id));
    }

    private void RebuildCategoryTree()
    {
        // Rebuilt on every mutation; preserves the current expansion and selection.
        var expandedIds = CollectNodes(CategoryTree)
            .Where(node => node.Category is not null && node.IsExpanded)
            .Select(node => node.Category!.Id)
            .ToHashSet();
        var selectedCategoryId = _selectedCategoryNode?.Category?.Id;
        var selectedKind = _selectedCategoryNode?.Kind;

        CategoryTree.Clear();

        var allNode = CategoryNodeViewModel.CreateAllEntries(OnCategoryNodeSelected);
        allNode.EntryCount = Entries.Count(entry => !entry.IsArchived);
        CategoryTree.Add(allNode);

        var uncategorizedNode = CategoryNodeViewModel.CreateUncategorized(OnCategoryNodeSelected);
        uncategorizedNode.EntryCount = Entries.Count(entry => entry.CategoryIds.Count == 0 && !entry.IsArchived);
        CategoryTree.Add(uncategorizedNode);

        var archivesNode = CategoryNodeViewModel.CreateArchives(OnCategoryNodeSelected);
        archivesNode.EntryCount = Entries.Count(entry => entry.IsArchived);
        CategoryTree.Add(archivesNode);

        // Flatten gives a depth-first walk with each node's depth; a stack
        // is enough to reconstruct the nesting.
        var categoryOrder = CategoryOrderStore.LoadAll();
        var colorIndexes = CategoryHierarchy.ComputeColorIndexes(Categories);
        var colorOverrides = CategoryColorStore.LoadAll();
        var nodeStack = new List<CategoryNodeViewModel>();

        foreach (var (category, depth) in CategoryHierarchy.Flatten(Categories, categoryOrder))
        {
            var node = CategoryNodeViewModel.CreateForCategory(category, OnCategoryNodeSelected);
            node.IsExpanded = expandedIds.Contains(category.Id);
            node.Depth = depth;
            node.ColorBrush = new SolidColorBrush(
                CategoryColorResolver.Resolve(category, Categories, colorIndexes, colorOverrides));

            if (depth == 0)
            {
                CategoryTree.Add(node);
            }
            else
            {
                nodeStack[depth - 1].Children.Add(node);
            }

            if (nodeStack.Count > depth)
            {
                nodeStack[depth] = node;
                nodeStack.RemoveRange(depth + 1, nodeStack.Count - depth - 1);
            }
            else
            {
                nodeStack.Add(node);
            }
        }

        foreach (var rootNode in CategoryTree.Skip(3))
        {
            ComputeEntryCounts(rootNode);
        }

        RestoreSelection(selectedKind, selectedCategoryId, allNode);
        OnPropertyChanged(nameof(HasCategories));
    }

    private void RestoreSelection(
        CategoryNodeKind? selectedKind,
        Guid? selectedCategoryId,
        CategoryNodeViewModel allNode)
    {
        var nodeToSelect = selectedKind switch
        {
            CategoryNodeKind.Category => CollectNodes(CategoryTree)
                .FirstOrDefault(node => node.Category?.Id == selectedCategoryId),
            CategoryNodeKind.Uncategorized => CategoryTree[1],
            CategoryNodeKind.Archives => CategoryTree[2],
            _ => allNode
        } ?? allNode;

        // The previous node no longer exists: the callback will update the filter.
        _selectedCategoryNode = null;
        nodeToSelect.IsSelected = true;
    }

    // Counts entries in the subtree and returns the covered Ids, used by the parent.
    private HashSet<Guid> ComputeEntryCounts(CategoryNodeViewModel node)
    {
        var subtreeIds = new HashSet<Guid> { node.Category!.Id };

        foreach (var child in node.Children)
        {
            subtreeIds.UnionWith(ComputeEntryCounts(child));
        }

        node.EntryCount = Entries.Count(entry => !entry.IsArchived && entry.CategoryIds.Any(subtreeIds.Contains));
        return subtreeIds;
    }

    private static IEnumerable<CategoryNodeViewModel> CollectNodes(
        IEnumerable<CategoryNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var descendant in CollectNodes(node.Children))
            {
                yield return descendant;
            }
        }
    }

    private string GetCategoryPath(VocabularyCategory category)
    {
        var categoriesById = Categories.ToDictionary(item => item.Id);
        var segments = new List<string> { category.Name };
        var visited = new HashSet<Guid> { category.Id };
        var current = category;

        while (current.ParentId is Guid parentId &&
               categoriesById.TryGetValue(parentId, out var parent) &&
               visited.Add(parent.Id))
        {
            segments.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" › ", segments);
    }

    private static VocabularyDatabase CreateSampleDatabase()
    {
        var durationCategory = new VocabularyCategory
        {
            Name = "Durée"
        };
        var literatureCategory = new VocabularyCategory
        {
            Name = "Littérature"
        };
        var characterCategory = new VocabularyCategory
        {
            Name = "Caractère"
        };
        var philosophyCategory = new VocabularyCategory
        {
            Name = "Philosophie"
        };

        return new VocabularyDatabase
        {
            Categories =
            [
                durationCategory,
                literatureCategory,
                characterCategory,
                philosophyCategory
            ],
            Entries =
            [
                new VocabularyEntry
            {
                Word = "Éphémère",
                Definition = "Qui ne dure qu'un temps très court.",
                Synonyms = { "passager", "fugace", "momentané" },
                ExampleSentences =
                {
                    "La beauté éphémère des cerisiers annonce le printemps."
                },
                Notes = "À rapprocher de fugace, qui insiste sur la rapidité.",
                Source = "Le Petit Prince — Antoine de Saint-Exupéry",
                CategoryIds = { durationCategory.Id, literatureCategory.Id }
            },
                new VocabularyEntry
            {
                Word = "Perspicace",
                Definition = "Qui comprend rapidement et avec justesse.",
                Synonyms = { "clairvoyant", "sagace", "lucide" },
                ExampleSentences =
                {
                    "Son observation perspicace révéla un détail ignoré de tous."
                },
                Source = "Lecture personnelle",
                CategoryIds = { characterCategory.Id }
            },
                new VocabularyEntry
            {
                Word = "Liminal",
                Definition = "Qui se situe à la limite d'un seuil ou entre deux états.",
                Synonyms = { "intermédiaire", "transitoire" },
                ExampleSentences =
                {
                    "Le personnage traverse un espace liminal entre rêve et réalité."
                },
                Notes = "Terme fréquent en anthropologie et en critique littéraire.",
                Source = "Essai sur les espaces de transition",
                CategoryIds = { philosophyCategory.Id, literatureCategory.Id }
            }
            ]
        };
    }

    private void SaveDatabase()
    {
        _repository.SaveDatabase(new VocabularyDatabase
        {
            Entries = Entries.ToList(),
            Categories = Categories.ToList(),
            PendingEntryDeletions = _pendingEntryDeletions,
            PendingCategoryDeletions = _pendingCategoryDeletions
        });
    }

    private void OnEntriesChanged()
    {
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(EmptyListMessage));
        OnPropertyChanged(nameof(EmptyDetailMessage));
    }

    private void RefreshFilteredEntries()
    {
        var selectedEntryId = SelectedEntry?.Id;
        _activeCategoryFilterIds = BuildCategoryFilterIds();

        var matchingEntries = Entries
            .Where(entry => EntryMatchesCategory(entry) && EntryMatchesSearch(entry))
            .ToList();

        FilteredEntries.Clear();

        foreach (var entry in matchingEntries)
        {
            FilteredEntries.Add(entry);
        }

        SearchStatusText = BuildSearchStatusText();
        OnPropertyChanged(nameof(HasFilteredEntries));
        OnPropertyChanged(nameof(EmptyListMessage));
        OnPropertyChanged(nameof(EmptyDetailMessage));

        var stillVisible = selectedEntryId is null
            ? null
            : FilteredEntries.FirstOrDefault(entry => entry.Id == selectedEntryId);

        SelectedEntry = stillVisible ?? FilteredEntries.FirstOrDefault();
    }

    // Set of Ids covered by the selected node (category + descendants), or
    // null when no category filter applies.
    private HashSet<Guid>? BuildCategoryFilterIds()
    {
        if (_selectedCategoryNode?.Category is not VocabularyCategory category)
        {
            return null;
        }

        var ids = CategoryHierarchy.GetDescendantIds(Categories, category.Id);
        ids.Add(category.Id);
        return ids;
    }

    private bool EntryMatchesCategory(VocabularyEntry entry)
    {
        // Archived entries are invisible everywhere except the Archives node
        // itself — checked first so it short-circuits every other branch.
        if (_selectedCategoryNode?.Kind == CategoryNodeKind.Archives)
        {
            return entry.IsArchived;
        }

        if (entry.IsArchived)
        {
            return false;
        }

        if (_selectedCategoryNode is null || _selectedCategoryNode.Kind == CategoryNodeKind.AllEntries)
        {
            return true;
        }

        if (_selectedCategoryNode.Kind == CategoryNodeKind.Uncategorized)
        {
            return entry.CategoryIds.Count == 0;
        }

        return _activeCategoryFilterIds is not null &&
            entry.CategoryIds.Any(_activeCategoryFilterIds.Contains);
    }

    private string BuildSearchStatusText()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            var count = FilteredEntries.Count;
            return $"{count} mot{(count > 1 ? "s" : "")}";
        }

        return FilteredEntries.Count == 0
            ? "Aucun résultat"
            : $"{FilteredEntries.Count} résultat(s)";
    }

    private bool EntryMatchesSearch(VocabularyEntry entry)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        var normalizedQuery = NormalizeForSearch(SearchQuery);

        // Search stays deliberately simple: no index, just an in-memory
        // scan — enough for Phase 1 and a few hundred entries.
        return SearchFieldMatches(entry.Word, normalizedQuery) ||
            SearchFieldMatches(entry.Definition, normalizedQuery) ||
            SearchFieldMatches(entry.Notes, normalizedQuery) ||
            SearchFieldMatches(entry.Source, normalizedQuery) ||
            SearchFieldsMatch(entry.Synonyms, normalizedQuery) ||
            SearchFieldsMatch(entry.ExampleSentences, normalizedQuery) ||
            SearchFieldsMatch(GetCategoryNames(entry.CategoryIds), normalizedQuery);
    }

    private IEnumerable<string> GetCategoryNames(IEnumerable<Guid> categoryIds)
    {
        var categoriesById = Categories.ToDictionary(category => category.Id);

        foreach (var categoryId in categoryIds)
        {
            if (categoriesById.TryGetValue(categoryId, out var category))
            {
                yield return category.Name;
            }
        }
    }

    private IEnumerable<VocabularyCategory> GetCategories(IEnumerable<Guid> categoryIds)
    {
        var categoriesById = Categories.ToDictionary(category => category.Id);

        foreach (var categoryId in categoryIds)
        {
            if (categoriesById.TryGetValue(categoryId, out var category))
            {
                yield return category;
            }
        }
    }

    private static bool SearchFieldsMatch(IEnumerable<string> values, string normalizedQuery)
    {
        return values.Any(value => SearchFieldMatches(value, normalizedQuery));
    }

    private static bool SearchFieldMatches(string value, string normalizedQuery)
    {
        return NormalizeForSearch(value)
            .Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForSearch(string value)
    {
        // Strips accents before comparing ("ephemere" matches "Éphémère").
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private int FindEntryIndex(Guid entryId)
    {
        for (var index = 0; index < Entries.Count; index++)
        {
            if (Entries[index].Id == entryId)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindCategoryIndex(Guid categoryId)
    {
        for (var index = 0; index < Categories.Count; index++)
        {
            if (Categories[index].Id == categoryId)
            {
                return index;
            }
        }

        return -1;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
