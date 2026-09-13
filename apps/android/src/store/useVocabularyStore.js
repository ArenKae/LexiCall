import { randomUUID } from 'expo-crypto';
import { create } from 'zustand';
import { normalizeCategory, normalizeEntry } from '../models/vocabulary';
import { createApiClient } from '../services/apiClient';
import {
  importDatabaseFrom,
  loadDatabase,
  loadSettings,
  resetLocalData,
  saveDatabase,
  saveSettings,
  stageDatabaseExport,
} from '../services/storage';
import { loadSyncHistory, saveSyncHistory, MAX_HISTORY_ENTRIES } from '../services/syncHistoryStore';
import { getDescendantIds, getSiblingsInOrder } from '../utils/categoryHierarchy';
import { ALL_ENTRIES, SORT_RECENT } from '../utils/filterEntries';
import { UNDEFINED_TYPE } from '../utils/vocabularyEntryTypes';
import { mergePulled } from './mergePulled';
import { runSyncCycle } from './syncCycle';

// In-memory vocabulary state, hydrated from the local JSON files, pushed to the
// API on every mutation and refreshed by delta pulls.

// Checkpoint standing for "pull everything". Not null: omitting updated_since
// returns the live view *without* tombstones, so records deleted on the server
// but still lingering locally would never be cleaned up.
const FULL_PULL_CHECKPOINT = '1970-01-01T00:00:00Z';

// CreatedAt and ClientLastWrite are stamped from the same instant at
// creation, so any difference means at least one edit happened since.
function changeKind(record) {
  return record.CreatedAt === record.ClientLastWrite ? 'Created' : 'Updated';
}

function normalizeBaseUrl(baseUrl) {
  return (baseUrl ?? '').trim().replace(/\/+$/, '').toLowerCase();
}

export const useVocabularyStore = create((set, get) => {
  // Bumped by importDatabase: a resync started before an import must drop its
  // whole batch instead of writing the pre-import state back over the freshly
  // imported file.
  let dataGeneration = 0;
  let resyncRequestedWhileSyncing = false;
  let fullResyncRequested = false;

  function persist(patch) {
    const next = { ...get(), ...patch };
    set(patch);
    saveDatabase({
      Entries: next.entries,
      Categories: next.categories,
      PendingEntryDeletions: next.pendingEntryDeletions,
      PendingCategoryDeletions: next.pendingCategoryDeletions,
    });
  }

  function recordHistory(rows) {
    if (rows.length === 0) {
      return;
    }

    const syncHistory = [
      ...rows.map((row) => ({ ...row, Id: randomUUID(), Timestamp: new Date().toISOString() })),
      ...get().syncHistory,
    ].slice(0, MAX_HISTORY_ENTRIES);

    set({ syncHistory });
    saveSyncHistory(syncHistory);
  }

  function client() {
    const { apiBaseUrl, apiKey } = get();
    return createApiClient(apiBaseUrl, apiKey);
  }

  // Drops everything describing a relationship with one specific server: every
  // record is then pushed again and the whole server view pulled back, both
  // still arbitrated per record by Last-Write-Wins.
  async function invalidateSyncState() {
    const syncedAgainstBaseUrl = normalizeBaseUrl(get().apiBaseUrl);

    persist({
      entries: get().entries.map((entry) => ({ ...entry, SyncedAt: null })),
      categories: get().categories.map((category) => ({ ...category, SyncedAt: null })),
    });
    set({ lastPulledAt: FULL_PULL_CHECKPOINT, syncedAgainstBaseUrl });
    await saveSettings({ lastPulledAt: FULL_PULL_CHECKPOINT, syncedAgainstBaseUrl });
  }

  // A parent that would create a cycle, or a name already used by a sibling —
  // checked against live state (not a snapshot the form opened with), since
  // that's what a save actually lands against. categoryId is null for a
  // not-yet-created category: it can't be its own ancestor either way, so the
  // cycle check is naturally a no-op there.
  function validateCategory(categoryId, name, parentId) {
    const categories = get().categories;

    if (parentId) {
      const descendantIds = categoryId ? getDescendantIds(categories, categoryId) : new Set();
      if (parentId === categoryId || descendantIds.has(parentId)) {
        return 'Le parent choisi créerait un cycle dans la hiérarchie.';
      }
    }

    const duplicateExists = categories.some(
      (category) =>
        category.Id !== categoryId &&
        category.ParentId === parentId &&
        category.Name.toLowerCase() === name.toLowerCase()
    );

    return duplicateExists ? 'Une catégorie porte déjà ce nom au même niveau.' : null;
  }

  // Reorders one sibling group and persists the resulting rank for the whole
  // group — a local device preference (categoryOrder), never synced.
  function moveCategory(categoryId, offset) {
    const category = get().categories.find((item) => item.Id === categoryId);

    if (!category) {
      return;
    }

    const siblings = getSiblingsInOrder(get().categories, category, get().categoryOrder);
    const index = siblings.findIndex((sibling) => sibling.Id === categoryId);
    const targetIndex = index + offset;

    if (targetIndex < 0 || targetIndex >= siblings.length) {
      return;
    }

    [siblings[index], siblings[targetIndex]] = [siblings[targetIndex], siblings[index]];

    const categoryOrder = { ...get().categoryOrder };
    siblings.forEach((sibling, rank) => {
      categoryOrder[sibling.Id] = rank;
    });

    set({ categoryOrder });
    saveSettings({ categoryOrder });
  }

  // Marks a record synced only if it hasn't been edited again since the push
  // started — otherwise the newer version would be wrongly considered sent.
  function markSynced(collectionKey, record) {
    const collection = get()[collectionKey].map((item) =>
      item.Id === record.Id && item.ClientLastWrite === record.ClientLastWrite
        ? { ...item, SyncedAt: record.ClientLastWrite }
        : item
    );
    persist({ [collectionKey]: collection });
  }

  async function pushUpsert(entityType, record) {
    const api = client();

    if (!api.isConfigured()) {
      return;
    }

    const collectionKey = entityType === 'Entry' ? 'entries' : 'categories';
    const label = entityType === 'Entry' ? record.Word : record.Name;
    const success =
      entityType === 'Entry' ? await api.upsertEntry(record) : await api.upsertCategory(record);

    if (success) {
      markSynced(collectionKey, record);
    }
    recordHistory([
      {
        EntityType: entityType,
        EntityId: record.Id,
        EntityLabel: label,
        Operation: 'Push',
        Outcome: success ? 'Success' : 'Failure',
        ChangeKind: changeKind(record),
      },
    ]);
  }

  async function pushDeletion(entityType, pending) {
    const api = client();

    if (!api.isConfigured()) {
      return;
    }

    const queueKey =
      entityType === 'Entry' ? 'pendingEntryDeletions' : 'pendingCategoryDeletions';
    const success =
      entityType === 'Entry'
        ? await api.deleteEntry(pending.Id, pending.DeletedAt)
        : await api.deleteCategory(pending.Id, pending.DeletedAt);

    if (success) {
      persist({ [queueKey]: get()[queueKey].filter((item) => item.Id !== pending.Id) });
    }
    recordHistory([
      {
        EntityType: entityType,
        EntityId: pending.Id,
        EntityLabel: pending.Label,
        Operation: 'Delete',
        Outcome: success ? 'Success' : 'Failure',
      },
    ]);
  }

  return {
    entries: [],
    categories: [],
    pendingEntryDeletions: [],
    pendingCategoryDeletions: [],
    syncHistory: [],
    apiBaseUrl: '',
    apiKey: '',
    lastPulledAt: null,
    syncedAgainstBaseUrl: null,
    isHydrated: false,
    isSyncing: false,
    // Drives the Options screen's "Tout resynchroniser" spinner and label.
    isFullResyncing: false,
    statusMessage: '',
    // NotConfigured | Syncing | Ok | Problem
    globalSyncStatus: 'NotConfigured',
    lastSyncedAt: null,
    // Set while an editor screen is open: a resync landing mid-edit would swap
    // the record out from under the form.
    isEditorOpen: false,
    // Which slice of the collection the browsing list shows. Not persisted: a
    // fresh launch always opens on everything.
    categoryFilter: { kind: ALL_ENTRIES, categoryId: null },
    searchQuery: '',
    // In-memory only, like the two above — never written to settings.json.
    sortMode: SORT_RECENT,
    // Manual category color/order overrides — local to this device, never
    // synced (see saveApiConfig-adjacent settings.json fields).
    categoryColors: {},
    categoryOrder: {},

    setCategoryFilter: (categoryFilter) => set({ categoryFilter }),
    setSearchQuery: (searchQuery) => set({ searchQuery }),
    setSortMode: (sortMode) => set({ sortMode }),
    setEditorOpen: (isEditorOpen) => set({ isEditorOpen }),

    hydrate: async () => {
      const [database, settings, syncHistory] = await Promise.all([
        loadDatabase(),
        loadSettings(),
        loadSyncHistory(),
      ]);

      set({
        entries: database.Entries,
        categories: database.Categories,
        pendingEntryDeletions: database.PendingEntryDeletions,
        pendingCategoryDeletions: database.PendingCategoryDeletions,
        syncHistory,
        apiBaseUrl: settings.apiBaseUrl,
        apiKey: settings.apiKey,
        lastPulledAt: settings.lastPulledAt,
        syncedAgainstBaseUrl: settings.syncedAgainstBaseUrl,
        categoryColors: settings.categoryColors,
        categoryOrder: settings.categoryOrder,
        isHydrated: true,
        // A resync fires as soon as hydration completes, so a configured client
        // is already syncing by the time this status is read.
        globalSyncStatus: settings.apiBaseUrl ? 'Syncing' : 'NotConfigured',
      });
    },

    saveApiConfig: async (apiBaseUrl, apiKey) => {
      const trimmedUrl = apiBaseUrl.trim();
      const trimmedKey = apiKey.trim();

      await saveSettings({ apiBaseUrl: trimmedUrl, apiKey: trimmedKey });
      set({ apiBaseUrl: trimmedUrl, apiKey: trimmedKey });
    },

    testConnection: async () => client().testConnection(),

    clearSyncHistory: () => {
      set({ syncHistory: [] });
      saveSyncHistory([]);
    },

    // Throws away everything local and reloads from the API on the next sync.
    resetLocalData: async () => {
      await resetLocalData();
      set({
        entries: [],
        categories: [],
        pendingEntryDeletions: [],
        pendingCategoryDeletions: [],
        lastPulledAt: null,
        lastSyncedAt: null,
        statusMessage: 'Données locales effacées.',
      });
    },

    resync: async () => {
      if (get().isEditorOpen) {
        return;
      }

      // A cycle already running defers this one to right after it, rather than
      // dropping it: a full resync or an import must not wait a whole tick.
      if (get().isSyncing) {
        resyncRequestedWhileSyncing = true;
        return;
      }

      const api = client();

      if (!api.isConfigured()) {
        set({ globalSyncStatus: 'NotConfigured' });
        return;
      }

      set({ isSyncing: true, globalSyncStatus: 'Syncing' });

      // Every exit below is the cycle's own, not the action's: the deferred
      // rerun after the finally must still get its turn.
      const runCycle = async () => {
        // Sync state built against another server describes nothing here, so
        // it is wiped and rebuilt — but only once this server has actually
        // answered, so a half-typed URL can't trigger it. Skipped entirely
        // when nothing ever pulled: there is no divergence to heal yet.
        if (normalizeBaseUrl(get().syncedAgainstBaseUrl) !== normalizeBaseUrl(get().apiBaseUrl)) {
          if ((await api.testConnection()) !== 'Ok') {
            set({ globalSyncStatus: 'Problem', statusMessage: 'API injoignable.' });
            return;
          }

          if (get().lastPulledAt === null) {
            const syncedAgainstBaseUrl = normalizeBaseUrl(get().apiBaseUrl);
            set({ syncedAgainstBaseUrl });
            await saveSettings({ syncedAgainstBaseUrl });
          } else {
            await invalidateSyncState();
            fullResyncRequested = true;
          }
        }

        const isFullResync = fullResyncRequested;
        fullResyncRequested = false;

        const generation = dataGeneration;
        const state = get();
        const result = await runSyncCycle({
          client: api,
          entries: state.entries,
          categories: state.categories,
          pendingEntryDeletions: state.pendingEntryDeletions,
          pendingCategoryDeletions: state.pendingCategoryDeletions,
          lastPulledAt: state.lastPulledAt,
        });

        if (!result.reachable) {
          // The request survives to the next cycle rather than being lost to
          // an outage that had nothing to do with it.
          fullResyncRequested = isFullResync;
          set({ globalSyncStatus: 'Problem', statusMessage: 'API injoignable.' });
          return;
        }

        const confirmedDeletions = new Set(
          result.deletions.filter((row) => row.success).map((row) => row.pending.Id)
        );
        const confirmedPushes = new Map(
          result.pushes.filter((row) => row.success).map((row) => [row.record.Id, row.record.ClientLastWrite])
        );

        const applySynced = (collection) =>
          collection.map((item) =>
            confirmedPushes.get(item.Id) === item.ClientLastWrite
              ? { ...item, SyncedAt: item.ClientLastWrite }
              : item
          );

        let entries = applySynced(get().entries);
        let categories = applySynced(get().categories);
        let appliedPulls = 0;
        const historyRows = [
          ...result.deletions.map((row) => ({
            EntityType: row.entityType,
            EntityId: row.pending.Id,
            EntityLabel: row.pending.Label,
            Operation: 'Delete',
            Outcome: row.success ? 'Success' : 'Failure',
          })),
          ...result.pushes.map((row) => ({
            EntityType: row.entityType,
            EntityId: row.record.Id,
            EntityLabel: row.entityType === 'Entry' ? row.record.Word : row.record.Name,
            Operation: 'Push',
            Outcome: row.success ? 'Success' : 'Failure',
            ChangeKind: changeKind(row.record),
          })),
        ];

        if (result.pull) {
          const pulledCategories = result.pull.categories.map(normalizeCategory);
          const pulledEntries = result.pull.entries.map(normalizeEntry);

          const pullRows = (records, entityType, localCollection) =>
            records
              // A tombstone for something never held locally changed nothing:
              // no row for it at all.
              .filter((record) => !record.IsDeleted || localCollection.some((item) => item.Id === record.Id))
              .map((record) => ({
                EntityType: entityType,
                EntityId: record.Id,
                EntityLabel: entityType === 'Entry' ? record.Word : record.Name,
                // A tombstone that actually removes something locally reads as
                // a deletion, not a pull — the history goes by effect.
                Operation: record.IsDeleted ? 'Delete' : 'Pull',
                Outcome: 'Success',
                ChangeKind: record.IsDeleted ? undefined : changeKind(record),
              }));

          historyRows.push(...pullRows(pulledCategories, 'Category', categories));
          historyRows.push(...pullRows(pulledEntries, 'Entry', entries));

          const mergedCategories = mergePulled(categories, pulledCategories);
          const mergedEntries = mergePulled(entries, pulledEntries);
          categories = mergedCategories.records;
          entries = mergedEntries.records;
          appliedPulls = mergedCategories.applied + mergedEntries.applied;

          if (generation === dataGeneration) {
            await saveSettings({ lastPulledAt: result.pull.checkpoint });
          }
        }

        // An import landed while this cycle was in flight: its whole batch
        // describes the replaced database, so none of it may be written back.
        if (generation !== dataGeneration) {
          return;
        }

        persist({
          entries,
          categories,
          pendingEntryDeletions: get().pendingEntryDeletions.filter(
            (item) => !confirmedDeletions.has(item.Id)
          ),
          pendingCategoryDeletions: get().pendingCategoryDeletions.filter(
            (item) => !confirmedDeletions.has(item.Id)
          ),
        });
        set({
          lastPulledAt: result.pull ? result.pull.checkpoint : get().lastPulledAt,
          globalSyncStatus: result.pull ? 'Ok' : 'Problem',
          lastSyncedAt: new Date().toISOString(),
          statusMessage: result.pull
            ? `${result.pull.entries.length} entrée(s) et ${result.pull.categories.length} catégorie(s) reçues.`
            : 'Échec du rapatriement.',
        });
        // A full resync touches every record, which would blow the history cap
        // and erase everything else — one summary row replaces the hundreds of
        // per-record ones.
        if (isFullResync) {
          const pushed = result.pushes.filter((row) => row.success).length;
          const failures = result.pushes.length - pushed;

          recordHistory([
            {
              // No entity: this row describes a whole cycle, not one record.
              EntityType: 'Entry',
              EntityId: null,
              EntityLabel: 'Resynchronisation complète',
              Operation: 'FullResync',
              Outcome: failures > 0 ? 'Failure' : 'Success',
              Details:
                `${pushed} envoyé(s) · ${appliedPulls} reçu(s)` +
                (failures > 0 ? ` · ${failures} échec(s)` : ''),
            },
          ]);
        } else {
          recordHistory(historyRows);
        }
      };

      try {
        await runCycle();
      } finally {
        set({ isSyncing: false });
      }

      if (resyncRequestedWhileSyncing) {
        resyncRequestedWhileSyncing = false;
        await get().resync();
      }
    },

    // Wipes every trace of a past relationship with the configured server and
    // runs one cycle: everything local is pushed again, the server's whole
    // view pulled back, each record still arbitrated by Last-Write-Wins.
    forceFullResync: async () => {
      if (!client().isConfigured()) {
        set({ globalSyncStatus: 'NotConfigured' });
        return;
      }

      await invalidateSyncState();
      fullResyncRequested = true;
      set({ isFullResyncing: true });

      try {
        await get().resync();
      } finally {
        set({ isFullResyncing: false });
      }
    },

    // Hands the database to the share sheet as a dated copy; returns the uri
    // the caller shares, since sharing itself is a UI concern.
    exportDatabase: () => {
      const state = get();

      return stageDatabaseExport({
        Entries: state.entries,
        Categories: state.categories,
        PendingEntryDeletions: state.pendingEntryDeletions,
        PendingCategoryDeletions: state.pendingCategoryDeletions,
      });
    },

    // Replaces the local database with a picked file, then resyncs. The forced
    // checkpoint matters: imported records carry their own SyncedAt, so
    // without it the import would neither push nor pull and would sit
    // silently out of sync forever.
    importDatabase: async (sourceFile) => {
      const database = await importDatabaseFrom(sourceFile);

      dataGeneration++;
      set({
        entries: database.Entries,
        categories: database.Categories,
        pendingEntryDeletions: database.PendingEntryDeletions,
        pendingCategoryDeletions: database.PendingCategoryDeletions,
        lastPulledAt: FULL_PULL_CHECKPOINT,
        categoryFilter: { kind: ALL_ENTRIES, categoryId: null },
        searchQuery: '',
        statusMessage: `${database.Entries.length} entrée(s) importée(s).`,
      });
      await saveSettings({ lastPulledAt: FULL_PULL_CHECKPOINT });

      get().resync();
      return database;
    },

    addEntry: (draft) => {
      const now = new Date().toISOString();
      const entry = {
        ...draft,
        Id: randomUUID(),
        CreatedAt: now,
        ClientLastWrite: now,
        IsDeleted: false,
        SyncedAt: null,
      };

      persist({ entries: [...get().entries, entry] });
      pushUpsert('Entry', entry);
      return entry;
    },

    updateEntry: (id, draft) => {
      const existing = get().entries.find((entry) => entry.Id === id);

      if (!existing) {
        return null;
      }

      const updated = {
        ...existing,
        ...draft,
        Id: existing.Id,
        CreatedAt: existing.CreatedAt,
        ClientLastWrite: new Date().toISOString(),
        SyncedAt: null,
      };

      persist({ entries: get().entries.map((entry) => (entry.Id === id ? updated : entry)) });
      pushUpsert('Entry', updated);
      return updated;
    },

    deleteEntry: (id) => {
      const entry = get().entries.find((item) => item.Id === id);

      if (!entry) {
        return;
      }

      // Queued before the push is attempted: the record is about to be gone
      // locally, so this is the only thing left that knows the server still
      // has to be told.
      const pending = { Id: id, DeletedAt: new Date().toISOString(), Label: entry.Word };

      persist({
        entries: get().entries.filter((item) => item.Id !== id),
        pendingEntryDeletions: [...get().pendingEntryDeletions, pending],
      });
      pushDeletion('Entry', pending);
    },

    toggleArchive: (id) => {
      const updated = get().entries.map((entry) =>
        entry.Id === id
          ? {
              ...entry,
              IsArchived: !entry.IsArchived,
              ClientLastWrite: new Date().toISOString(),
              SyncedAt: null,
            }
          : entry
      );

      persist({ entries: updated });
      const entry = updated.find((item) => item.Id === id);

      if (entry) {
        pushUpsert('Entry', entry);
      }
    },

    // Returns { error, category }: error is a message on failure (the screen
    // shows it and never navigates away, matching a failed entry save) or
    // null on success, with the created record — a categorization suggestion
    // accepted as "new category" needs the id it was just given.
    addCategory: (draft) => {
      const id = randomUUID();
      const error = validateCategory(id, draft.Name, draft.ParentId);

      if (error) {
        return { error, category: null };
      }

      const now = new Date().toISOString();
      const category = {
        ...draft,
        Id: id,
        CreatedAt: now,
        ClientLastWrite: now,
        IsDeleted: false,
        SyncedAt: null,
      };

      persist({ categories: [...get().categories, category] });
      pushUpsert('Category', category);
      return { error: null, category };
    },

    updateCategory: (id, draft) => {
      const existing = get().categories.find((category) => category.Id === id);

      if (!existing) {
        return null;
      }

      const error = validateCategory(id, draft.Name, draft.ParentId);

      if (error) {
        return error;
      }

      const updated = {
        ...existing,
        ...draft,
        Id: existing.Id,
        CreatedAt: existing.CreatedAt,
        ClientLastWrite: new Date().toISOString(),
        SyncedAt: null,
      };

      persist({
        categories: get().categories.map((category) => (category.Id === id ? updated : category)),
      });
      pushUpsert('Category', updated);
      return null;
    },

    // Same guardrails as the API's own delete route (subcategories, entries
    // still filed under it) — checked here first so the common case never
    // round-trips to find out.
    deleteCategory: (id) => {
      const categories = get().categories;
      const category = categories.find((item) => item.Id === id);

      if (!category) {
        return null;
      }

      if (categories.some((item) => item.ParentId === id)) {
        return 'Impossible de supprimer une catégorie qui contient des sous-catégories.';
      }

      const usageCount = get().entries.filter((entry) => entry.CategoryIds.includes(id)).length;

      if (usageCount > 0) {
        return `Impossible de supprimer : cette catégorie est utilisée par ${usageCount} mot(s).`;
      }

      const pending = { Id: id, DeletedAt: new Date().toISOString(), Label: category.Name };
      const categoryColors = { ...get().categoryColors };
      const categoryOrder = { ...get().categoryOrder };
      delete categoryColors[id];
      delete categoryOrder[id];

      persist({
        categories: categories.filter((item) => item.Id !== id),
        pendingCategoryDeletions: [...get().pendingCategoryDeletions, pending],
      });
      set({ categoryColors, categoryOrder });
      saveSettings({ categoryColors, categoryOrder });
      pushDeletion('Category', pending);
      return null;
    },

    moveCategoryUp: (id) => moveCategory(id, -1),
    moveCategoryDown: (id) => moveCategory(id, 1),

    // Null clears the override and reverts to the automatic golden-angle hue.
    setCategoryColor: (categoryId, hexColor) => {
      const categoryColors = { ...get().categoryColors };

      if (hexColor) {
        categoryColors[categoryId] = hexColor;
      } else {
        delete categoryColors[categoryId];
      }

      set({ categoryColors });
      saveSettings({ categoryColors });
    },
  };
});

// A blank entry draft, ready for the editor to fill in and pass to addEntry.
export function createEntryDraft(initialCategoryId) {
  return {
    Word: '',
    Type: [UNDEFINED_TYPE],
    Definition: [''],
    CategoryIds: initialCategoryId ? [initialCategoryId] : [],
    Synonyms: [],
    ExampleSentences: [],
    Notes: '',
    Source: '',
    Images: [],
    IsArchived: false,
    LockedFields: [],
  };
}

// A blank category draft, ready for the editor to fill in and pass to
// addCategory. initialParentId pre-selects a parent when creating a
// subcategory from an existing node.
export function createCategoryDraft(initialParentId) {
  return {
    Name: '',
    ParentId: initialParentId ?? null,
    Description: '',
    IconGlyph: '',
  };
}
