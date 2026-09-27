import Feather from '@expo/vector-icons/Feather';
import { useCallback, useDeferredValue, useMemo, useRef, useState } from 'react';
import { useRouter } from 'expo-router';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { CloseButton } from '../../src/components/CloseButton';
import { EntryActionSheet } from '../../src/components/EntryActionSheet';
import { EntryCard } from '../../src/components/EntryCard';
import { FilterSheet } from '../../src/components/FilterSheet';
import { ScrollJumpButtons } from '../../src/components/ScrollJumpButtons';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { useScrollEdges } from '../../src/hooks/useScrollEdges';
import { lockedFieldsFor } from '../../src/models/vocabulary';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import {
  ALL_ENTRIES,
  ARCHIVES,
  LOCKED,
  UNCATEGORIZED,
  selectEntries,
} from '../../src/utils/filterEntries';

const VIRTUAL_ICONS = {
  [UNCATEGORIZED]: 'Solar.tag',
  [ARCHIVES]: 'Phosphor.books',
  [LOCKED]: 'Phosphor.lock-key',
};

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
  const listRef = useRef(null);

  const toggleLocks = useCallback(
    (entry, shouldLock) => updateEntry(entry.Id, { LockedFields: lockedFieldsFor(entry, shouldLock) }),
    [updateEntry]
  );

  const listQuery = useDeferredValue(query);
  const visible = useMemo(
    () => selectEntries({ entries, categories, filter, query: listQuery, sortMode }),
    [entries, categories, filter, listQuery, sortMode]
  );
  const scrollEdges = useScrollEdges(listRef, visible.length);

  const hideArchivedBadge = filter.kind === ARCHIVES;
  const openEntry = useCallback((entry) => router.push(`/entry/${entry.Id}`), [router]);
  const showActions = useCallback((entry) => setActionsFor(entry.Id), []);
  const renderEntry = useCallback(
    ({ item }) => (
      <EntryCard
        entry={item}
        categoryIndex={categoryIndex}
        onPress={openEntry}
        onLongPress={showActions}
        onToggleLocks={toggleLocks}
        hideArchivedBadge={hideArchivedBadge}
      />
    ),
    [categoryIndex, openEntry, showActions, toggleLocks, hideArchivedBadge]
  );

  const activeCategory = filter.categoryId ? categoryIndex.get(filter.categoryId) : null;
  const actionsEntry = actionsFor ? entries.find((entry) => entry.Id === actionsFor) : null;
  const filterLabel = activeCategory ? activeCategory.Name : VIRTUAL_LABELS[filter.kind];
  const clearFilter = () => setCategoryFilter({ kind: ALL_ENTRIES, categoryId: null });
  const status = listQuery.trim().length > 0
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
          {query.length > 0 && <CloseButton size={22} onPress={() => setSearchQuery('')} />}
          <View style={[styles.separator, { backgroundColor: colors.borderSubtle }]} />
          <Pressable onPress={() => setFilterSheetVisible(true)} hitSlop={8}>
            <Feather name="sliders" size={18} color={colors.textMuted} />
          </Pressable>
        </View>

        {filter.kind !== ALL_ENTRIES ? (
          <Pressable
            style={[styles.filterCard, { backgroundColor: colors.surface, borderColor: colors.accent }]}
            onPress={clearFilter}
          >
            <CategoryIcon
              iconKey={activeCategory ? activeCategory.icon : VIRTUAL_ICONS[filter.kind]}
              color={activeCategory ? activeCategory.color : colors.accent}
              size={20}
            />
            <View style={styles.filterText}>
              <Text style={[styles.filterName, { color: colors.accent }]} numberOfLines={2}>
                {filterLabel}
              </Text>
              <Text style={[styles.filterCount, { color: colors.textMuted }]}>{status}</Text>
            </View>
            <CloseButton size={28} onPress={clearFilter} />
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
      />

      <FlatList
        ref={listRef}
        data={visible}
        keyExtractor={(entry) => entry.Id}
        contentContainerStyle={styles.list}
        {...scrollEdges.listProps}
        ListEmptyComponent={
          <Text style={[styles.empty, { color: colors.textSecondary }]}>
            {isHydrated
              ? 'Aucune entrée à afficher.'
              : 'Chargement…'}
          </Text>
        }
        renderItem={renderEntry}
        strictMode
      />

      <ScrollJumpButtons
        edges={scrollEdges.edges}
        onScrollToTop={scrollEdges.scrollToTop}
        onScrollToBottom={scrollEdges.scrollToBottom}
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
  filterCard: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderWidth: 1.5,
    borderRadius: 12,
    paddingLeft: 12,
    paddingRight: 8,
    paddingVertical: 9,
  },
  filterText: { flex: 1, gap: 2 },
  filterName: { fontSize: 15, fontWeight: '700' },
  filterCount: { fontSize: 12 },
  list: { paddingTop: 6, paddingBottom: 64 },
  empty: { padding: 28, textAlign: 'center' },
});
