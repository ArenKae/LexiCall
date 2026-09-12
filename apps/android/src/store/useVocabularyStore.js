import { randomUUID } from 'expo-crypto';
import { create } from 'zustand';
import { normalizeCategory, normalizeEntry } from '../models/vocabulary';
import { createApiClient } from '../services/apiClient';
import {
  loadDatabase,
  loadSettings,
  resetLocalData,
  saveDatabase,
  saveSettings,
} from '../services/storage';
import { loadSyncHistory, saveSyncHistory, MAX_HISTORY_ENTRIES } from '../services/syncHistoryStore';
import { ALL_ENTRIES, SORT_RECENT } from '../utils/filterEntries';
import { UNDEFINED_TYPE } from '../utils/vocabularyEntryTypes';
import { mergePulled } from './mergePulled';
import { runSyncCycle } from './syncCycle';

// In-memory vocabulary state, hydrated from the local JSON files, pushed to the
// API on every mutation and refreshed by delta pulls.

// CreatedAt and ClientLastWrite are stamped from the same instant at
// creation, so any difference means at least one edit happened since.
function changeKind(record) {
  return record.CreatedAt === record.ClientLastWrite ? 'Created' : 'Updated';
}

export const useVocabularyStore = create((set, get) => {
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
    isHydrated: false,
    isSyncing: false,
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
      const state = get();

      if (state.isSyncing || state.isEditorOpen) {
        return;
      }

      const api = client();

      if (!api.isConfigured()) {
        set({ globalSyncStatus: 'NotConfigured' });
        return;
      }

      set({ isSyncing: true, globalSyncStatus: 'Syncing' });

      try {
        const result = await runSyncCycle({
          client: api,
          entries: state.entries,
          categories: state.categories,
          pendingEntryDeletions: state.pendingEntryDeletions,
          pendingCategoryDeletions: state.pendingCategoryDeletions,
          lastPulledAt: state.lastPulledAt,
        });

        if (!result.reachable) {
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

          categories = mergePulled(categories, pulledCategories);
          entries = mergePulled(entries, pulledEntries);
          await saveSettings({ lastPulledAt: result.pull.checkpoint });
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
        recordHistory(historyRows);
      } finally {
        set({ isSyncing: false });
      }
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
