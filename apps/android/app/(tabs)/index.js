import Feather from '@expo/vector-icons/Feather';
import { useMemo, useState } from 'react';
import { useRouter } from 'expo-router';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { EntryActionSheet } from '../../src/components/EntryActionSheet';
import { EntryCard } from '../../src/components/EntryCard';
import { FilterSheet } from '../../src/components/FilterSheet';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { LOCKABLE_FIELDS } from '../../src/models/vocabulary';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import {
  ALL_ENTRIES,
  ARCHIVES,
  LOCKED,
  UNCATEGORIZED,
  selectEntries,
} from '../../src/utils/filterEntries';

const VIRTUAL_LABELS = {
  [ALL_ENTRIES]: 'Toutes les entrées',
  [UNCATEGORIZED]: 'Sans catégorie',
  [ARCHIVES]: 'Archives',
  [LOCKED]: 'Verrouillées',
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
  const sortMode = useVocabularyStore((state) => state.sortMode);
  const setSearchQuery = useVocabularyStore((state) => state.setSearchQuery);
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);
  const setSortMode = useVocabularyStore((state) => state.setSortMode);
  const isHydrated = useVocabularyStore((state) => state.isHydrated);
  const toggleArchive = useVocabularyStore((state) => state.toggleArchive);
  const deleteEntry = useVocabularyStore((state) => state.deleteEntry);
  const updateEntry = useVocabularyStore((state) => state.updateEntry);
  const [filterSheetVisible, setFilterSheetVisible] = useState(false);
  const [actionsFor, setActionsFor] = useState(null);

  function toggleLocks(entry, shouldLock) {
    const kept = entry.LockedFields.filter((field) => !LOCKABLE_FIELDS.includes(field));
    updateEntry(entry.Id, {
      LockedFields: shouldLock ? [...kept, ...LOCKABLE_FIELDS] : kept,
    });
  }

  const visible = useMemo(
    () => selectEntries({ entries, categories, filter, query, sortMode }),
    [entries, categories, filter, query, sortMode]
  );

  const activeCategory = filter.categoryId ? categoryIndex.get(filter.categoryId) : null;
  const actionsEntry = actionsFor ? entries.find((entry) => entry.Id === actionsFor) : null;
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
          <View style={[styles.separator, { backgroundColor: colors.borderSubtle }]} />
          <Pressable onPress={() => setFilterSheetVisible(true)} hitSlop={8}>
            <Feather name="sliders" size={18} color={colors.textMuted} />
          </Pressable>
        </View>

        {filter.kind !== ALL_ENTRIES ? (
          // A bordered, accent-colored control, not a dismissible chip: sized
          // and colored to read as a real navigation action (go back up a
          // level) rather than a muted info label or a removable filter tag.
          <Pressable
            style={[styles.backControl, { backgroundColor: colors.surface, borderColor: colors.accent }]}
            onPress={() => setCategoryFilter({ kind: ALL_ENTRIES, categoryId: null })}
          >
            <CategoryIcon iconKey="Phosphor.caret-left" color={colors.accent} size={17} />
            <Text style={[styles.backControlText, { color: colors.accent }]} numberOfLines={1}>
              {filterLabel} · {status}
            </Text>
          </Pressable>
        ) : (
          <Text style={[styles.status, { color: colors.textMuted }]}>
            {filterLabel} · {status}
          </Text>
        )}
      </View>

      <FilterSheet
        visible={filterSheetVisible}
        onClose={() => setFilterSheetVisible(false)}
        sortMode={sortMode}
        onSortModeChange={setSortMode}
        filter={filter}
        filterLabel={filterLabel}
        activeCategory={activeCategory}
        onResetFilter={() => setCategoryFilter({ kind: ALL_ENTRIES, categoryId: null })}
      />

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
            onLongPress={() => setActionsFor(item.Id)}
            onToggleLocks={(shouldLock) => toggleLocks(item, shouldLock)}
            hideArchivedBadge={filter.kind === ARCHIVES}
          />
        )}
      />

      {actionsEntry && (
        <EntryActionSheet
          visible
          entry={actionsEntry}
          onClose={() => setActionsFor(null)}
          onEdit={() => {
            setActionsFor(null);
            router.push(`/entry/edit?id=${actionsEntry.Id}`);
          }}
          onToggleArchive={() => {
            setActionsFor(null);
            toggleArchive(actionsEntry.Id);
          }}
          onDelete={() => {
            setActionsFor(null);
            deleteEntry(actionsEntry.Id);
          }}
        />
      )}
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
  separator: { width: 1, height: 20, marginHorizontal: 2 },
  status: { fontSize: 12, paddingHorizontal: 2 },
  backControl: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    gap: 6,
    borderWidth: 1.5,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  backControlText: { fontSize: 14, fontWeight: '700' },
  list: { paddingTop: 6, paddingBottom: 16 },
  empty: { padding: 28, textAlign: 'center' },
});
