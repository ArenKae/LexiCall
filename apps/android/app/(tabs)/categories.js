import { useMemo, useState } from 'react';
import { useRouter } from 'expo-router';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { TreeChevron } from '../../src/components/TreeChevron';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { colorFromIndex } from '../../src/utils/categoryColor';
import { computeColorIndexes, flattenCategories } from '../../src/utils/categoryHierarchy';
import { ALL_ENTRIES, ARCHIVES, UNCATEGORIZED } from '../../src/utils/filterEntries';

const INDENT = 20;
const DEFAULT_CATEGORY_ICON = 'Solar.tag';

// Hides everything nested under a collapsed node: the depth-first order means a
// subtree is exactly the rows deeper than its root, up to the next shallower one.
function visibleRows(rows, expandedIds) {
  const visible = [];
  let collapsedAtDepth = null;

  for (const row of rows) {
    if (collapsedAtDepth !== null && row.depth > collapsedAtDepth) {
      continue;
    }
    collapsedAtDepth = null;
    visible.push(row);

    if (row.hasChildren && !expandedIds.has(row.key)) {
      collapsedAtDepth = row.depth;
    }
  }

  return visible;
}

// Category tree, preceded by the virtual selections. Picking a row sets the
// browsing filter and hands back to the list.
export default function Categories() {
  const colors = useTheme();
  const router = useRouter();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);
  const [expandedIds, setExpandedIds] = useState(() => new Set());

  const rows = useMemo(() => {
    const colorIndexes = computeColorIndexes(categories);
    const flat = flattenCategories(categories);

    const uncategorizedCount = entries.filter(
      (entry) => entry.CategoryIds.length === 0 && !entry.IsArchived
    ).length;
    const archivedCount = entries.filter((entry) => entry.IsArchived).length;

    const virtualRows = [
      {
        key: ALL_ENTRIES,
        label: 'Toutes les entrées',
        iconKey: 'Phosphor.stack',
        count: entries.filter((entry) => !entry.IsArchived).length,
        filter: { kind: ALL_ENTRIES, categoryId: null },
      },
      // Both catch-all views disappear when they have nothing to show, rather
      // than offering a selection that lands on an empty list.
      ...(uncategorizedCount > 0
        ? [
            {
              key: UNCATEGORIZED,
              label: 'Sans catégorie',
              iconKey: 'Solar.tag',
              count: uncategorizedCount,
              filter: { kind: UNCATEGORIZED, categoryId: null },
            },
          ]
        : []),
      ...(archivedCount > 0
        ? [
            {
              key: ARCHIVES,
              label: 'Archives',
              iconKey: 'Phosphor.books',
              count: archivedCount,
              filter: { kind: ARCHIVES, categoryId: null },
            },
          ]
        : []),
    ].map((row) => ({ ...row, depth: 0, hasChildren: false, color: colors.iconNeutral }));

    const categoryRows = flat.map(({ category, depth }, index) => ({
      key: category.Id,
      label: category.Name,
      iconKey: category.IconGlyph || DEFAULT_CATEGORY_ICON,
      depth,
      hasChildren: index + 1 < flat.length && flat[index + 1].depth > depth,
      color: colorFromIndex(colorIndexes.get(category.Id) ?? 0),
      filter: { kind: 'category', categoryId: category.Id },
    }));

    return [...virtualRows, ...categoryRows];
  }, [categories, entries, colors.iconNeutral]);

  const shown = useMemo(() => visibleRows(rows, expandedIds), [rows, expandedIds]);

  const toggle = (key) =>
    setExpandedIds((current) => {
      const next = new Set(current);
      if (!next.delete(key)) {
        next.add(key);
      }
      return next;
    });

  return (
    <FlatList
      data={shown}
      keyExtractor={(row) => row.key}
      contentContainerStyle={styles.list}
      renderItem={({ item }) => (
        <View style={[styles.row, { marginLeft: 14 + item.depth * INDENT }]}>
          <Pressable
            onPress={() => toggle(item.key)}
            hitSlop={10}
            style={styles.chevron}
            disabled={!item.hasChildren}
          >
            {item.hasChildren && (
              <TreeChevron expanded={expandedIds.has(item.key)} color={colors.textSecondary} />
            )}
          </Pressable>

          <Pressable
            style={[
              styles.rowBody,
              { backgroundColor: colors.surface, borderColor: colors.borderSubtle },
            ]}
            onPress={() => {
              setCategoryFilter(item.filter);
              router.push('/');
            }}
          >
            <CategoryIcon iconKey={item.iconKey} color={item.color} size={20} />
            <Text style={[styles.label, { color: colors.textPrimary }]} numberOfLines={2}>
              {item.label}
            </Text>
            {item.count !== undefined && (
              <View style={[styles.badge, { backgroundColor: colors.chipBackground }]}>
                <Text style={[styles.badgeText, { color: colors.chipForeground }]}>
                  {item.count}
                </Text>
              </View>
            )}
          </Pressable>
        </View>
      )}
    />
  );
}

const styles = StyleSheet.create({
  list: { paddingVertical: 10, paddingRight: 14 },
  row: { flexDirection: 'row', alignItems: 'center', marginBottom: 8 },
  chevron: { width: 22, alignItems: 'center', justifyContent: 'center' },
  rowBody: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  label: { flex: 1, fontSize: 15 },
  badge: { minWidth: 28, alignItems: 'center', borderRadius: 999, paddingHorizontal: 7, paddingVertical: 2 },
  badgeText: { fontSize: 12 },
});
