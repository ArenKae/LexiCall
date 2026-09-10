import { create } from 'zustand';
import { normalizeCategory, normalizeEntry } from '../models/vocabulary';
import { createApiClient } from '../services/apiClient';
import { loadDatabase, loadSettings, saveDatabase, saveSettings } from '../services/storage';
import { ALL_ENTRIES } from '../utils/filterEntries';
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

  setCategoryFilter: (categoryFilter) => set({ categoryFilter }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),

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
}));
