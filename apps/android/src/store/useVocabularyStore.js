import { create } from 'zustand';

// In-memory vocabulary state: entries, categories, search, category filter.
export const useVocabularyStore = create((set) => ({
  entries: [],
  categories: [],
  searchQuery: '',
  selectedCategoryId: null,

  setEntries: (entries) => set({ entries }),
  setCategories: (categories) => set({ categories }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),
  selectCategory: (categoryId) => set({ selectedCategoryId: categoryId }),
}));
