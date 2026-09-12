import { randomUUID } from 'expo-crypto';
import { create } from 'zustand';
import { normalizeCategory, normalizeEntry } from '../models/vocabulary';
import { createApiClient } from '../services/apiClient';
import { loadDatabase, loadSettings, saveDatabase, saveSettings } from '../services/storage';
import { ALL_ENTRIES, SORT_RECENT } from '../utils/filterEntries';
import { UNDEFINED_TYPE } from '../utils/vocabularyEntryTypes';
import { mergePulled } from './mergePulled';

// In-memory vocabulary state, hydrated from the local JSON files and refreshed
// by delta pulls from the API.

export const useVocabularyStore = create((set, get) => ({
  entries: [],
  categories: [],
  apiBaseUrl: '',
  apiKey: '',
  lastPulledAt: null,
  isHydrated: false,
  isSyncing: false,
  statusMessage: '',
  // Which slice of the collection the browsing list shows. Not persisted: a
  // fresh launch always opens on everything.
  categoryFilter: { kind: ALL_ENTRIES, categoryId: null },
  searchQuery: '',
  // In-memory only, like the two above — never written to settings.json.
  sortMode: SORT_RECENT,

  setCategoryFilter: (categoryFilter) => set({ categoryFilter }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),
  setSortMode: (sortMode) => set({ sortMode }),

  hydrate: async () => {
    const [database, settings] = await Promise.all([loadDatabase(), loadSettings()]);

    set({
      entries: database.Entries,
      categories: database.Categories,
      apiBaseUrl: settings.apiBaseUrl,
      apiKey: settings.apiKey,
      lastPulledAt: settings.lastPulledAt,
      isHydrated: true,
    });
  },

  saveApiConfig: async (apiBaseUrl, apiKey) => {
    const trimmedUrl = apiBaseUrl.trim();
    const trimmedKey = apiKey.trim();

    await saveSettings({ apiBaseUrl: trimmedUrl, apiKey: trimmedKey });
    set({ apiBaseUrl: trimmedUrl, apiKey: trimmedKey });
  },

  testConnection: async () => {
    const { apiBaseUrl, apiKey } = get();
    return createApiClient(apiBaseUrl, apiKey).testConnection();
  },

  syncNow: async () => {
    if (get().isSyncing) {
      return;
    }

    const { apiBaseUrl, apiKey, lastPulledAt } = get();
    set({ isSyncing: true, statusMessage: 'Synchronisation…' });

    try {
      const client = createApiClient(apiBaseUrl, apiKey);
      const pulledCategories = await client.pullCategories(lastPulledAt);
      const pulledEntries = await client.pullEntries(lastPulledAt);

      const categories = mergePulled(
        get().categories,
        pulledCategories.records.map(normalizeCategory)
      );
      const entries = mergePulled(get().entries, pulledEntries.records.map(normalizeEntry));

      // The categories pull ran first, so its token is the older of the two:
      // taking the entries one instead would skip over any category written
      // between the two requests, permanently.
      const checkpoint = pulledCategories.syncTimestamp ?? lastPulledAt;

      saveDatabase({ Entries: entries, Categories: categories });
      await saveSettings({ lastPulledAt: checkpoint });

      set({
        entries,
        categories,
        lastPulledAt: checkpoint,
        statusMessage:
          `${pulledEntries.records.length} entrée(s) et ` +
          `${pulledCategories.records.length} catégorie(s) reçues.`,
      });
    } catch (error) {
      set({ statusMessage: `Échec : ${error.message}` });
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
      UpdatedAt: now,
      IsDeleted: false,
      SyncedAt: null,
    };

    const entries = [...get().entries, entry];
    set({ entries });
    saveDatabase({ Entries: entries, Categories: get().categories });
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
      UpdatedAt: new Date().toISOString(),
      SyncedAt: null,
    };

    const entries = get().entries.map((entry) => (entry.Id === id ? updated : entry));
    set({ entries });
    saveDatabase({ Entries: entries, Categories: get().categories });
    return updated;
  },

  deleteEntry: (id) => {
    const entries = get().entries.filter((entry) => entry.Id !== id);
    set({ entries });
    saveDatabase({ Entries: entries, Categories: get().categories });
  },

  toggleArchive: (id) => {
    const entries = get().entries.map((entry) =>
      entry.Id === id
        ? { ...entry, IsArchived: !entry.IsArchived, UpdatedAt: new Date().toISOString() }
        : entry
    );
    set({ entries });
    saveDatabase({ Entries: entries, Categories: get().categories });
  },
}));

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
