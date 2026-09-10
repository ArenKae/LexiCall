import { useMemo } from 'react';
import { useRouter } from 'expo-router';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { EntryCard } from '../../src/components/EntryCard';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { ALL_ENTRIES, ARCHIVES, UNCATEGORIZED, selectEntries } from '../../src/utils/filterEntries';

const VIRTUAL_LABELS = {
  [ALL_ENTRIES]: 'Toutes les entrées',
  [UNCATEGORIZED]: 'Sans catégorie',
  [ARCHIVES]: 'Archives',
};

// Browsing list: the current category slice, narrowed by the search field.
export default function Home() {
  const colors = useTheme();
  const router = useRouter();
  const categoryIndex = useCategoryIndex();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const filter = useVocabularyStore((state) => state.categoryFilter);
  const query = useVocabularyStore((state) => state.searchQuery);
  const setSearchQuery = useVocabularyStore((state) => state.setSearchQuery);
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);
  const isHydrated = useVocabularyStore((state) => state.isHydrated);

  const visible = useMemo(
    () => selectEntries({ entries, categories, filter, query }),
    [entries, categories, filter, query]
  );

  const activeCategory = filter.categoryId ? categoryIndex.get(filter.categoryId) : null;
  const filterLabel = activeCategory ? activeCategory.Name : VIRTUAL_LABELS[filter.kind];
  const status = query.trim().length > 0
    ? `${visible.length} résultat${visible.length > 1 ? 's' : ''}`
    : `${visible.length} mot${visible.length > 1 ? 's' : ''}`;

  return (
    <View style={styles.container}>
      <View style={styles.toolbar}>
        <View
          style={[
            styles.searchField,
            { backgroundColor: colors.surface, borderColor: colors.borderSubtle },
          ]}
        >
          <CategoryIcon iconKey="Phosphor.magnifying-glass" color={colors.textMuted} size={17} />
          <TextInput
            style={[styles.searchInput, { color: colors.textPrimary }]}
            value={query}
            onChangeText={setSearchQuery}
            placeholder="Rechercher un mot…"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="none"
            autoCorrect={false}
          />
          {query.length > 0 && (
            <Pressable onPress={() => setSearchQuery('')} hitSlop={8}>
              <CategoryIcon iconKey="Phosphor.x" color={colors.textMuted} size={15} />
            </Pressable>
          )}
        </View>

        <View style={styles.filterRow}>
          <Pressable
            style={[styles.filterChip, { backgroundColor: colors.chipBackground }]}
            onPress={() => setCategoryFilter({ kind: ALL_ENTRIES, categoryId: null })}
            disabled={filter.kind === ALL_ENTRIES}
          >
            {activeCategory && (
              <View style={[styles.dot, { backgroundColor: activeCategory.color }]} />
            )}
            <Text style={[styles.filterText, { color: colors.chipForeground }]}>{filterLabel}</Text>
            {filter.kind !== ALL_ENTRIES && (
              <CategoryIcon iconKey="Phosphor.x" color={colors.chipForeground} size={12} />
            )}
          </Pressable>
          <Text style={[styles.status, { color: colors.textMuted }]}>{status}</Text>
        </View>
      </View>

      <FlatList
        data={visible}
        keyExtractor={(entry) => entry.Id}
        contentContainerStyle={styles.list}
        ListEmptyComponent={
          <Text style={[styles.empty, { color: colors.textSecondary }]}>
            {isHydrated
              ? 'Aucune entrée à afficher.'
              : 'Chargement…'}
          </Text>
        }
        renderItem={({ item }) => (
          <EntryCard
            entry={item}
            categoryIndex={categoryIndex}
            onPress={() => router.push(`/entry/${item.Id}`)}
          />
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  toolbar: { paddingHorizontal: 14, paddingTop: 10, paddingBottom: 6, gap: 8 },
  searchField: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    height: 42,
  },
  searchInput: { flex: 1, fontSize: 15, paddingVertical: 0 },
  filterRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  filterChip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 999,
  },
  filterText: { fontSize: 12, fontWeight: '600' },
  dot: { width: 8, height: 8, borderRadius: 999 },
  status: { fontSize: 12 },
  list: { paddingTop: 6, paddingBottom: 16 },
  empty: { padding: 28, textAlign: 'center' },
});
