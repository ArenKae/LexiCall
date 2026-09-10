import { useMemo, useState } from 'react';
import { useRouter } from 'expo-router';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { compareText } from '../../src/utils/collation';
import { entryMatchesSearch, normalizeForSearch } from '../../src/utils/search';

const SEGMENTS = [
  { key: 'all', label: 'Tous' },
  { key: 'words', label: 'Mots' },
  { key: 'categories', label: 'Catégories' },
];

// Search across words and categories at once, unconstrained by the browsing
// filter — a lookup, not a way to narrow the current list.
export default function Search() {
  const colors = useTheme();
  const router = useRouter();
  const categoryIndex = useCategoryIndex();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);
  const [query, setQuery] = useState('');
  const [segment, setSegment] = useState('all');

  const { words, matchedCategories } = useMemo(() => {
    const normalized = normalizeForSearch(query);

    if (normalized.trim().length === 0) {
      return { words: [], matchedCategories: [] };
    }

    const categoryNamesById = new Map(categories.map((category) => [category.Id, category.Name]));

    return {
      words: entries
        .filter((entry) => entryMatchesSearch(entry, normalized, categoryNamesById))
        .sort((a, b) => compareText(a.Word, b.Word)),
      matchedCategories: categories
        .filter((category) => normalizeForSearch(category.Name).includes(normalized))
        .sort((a, b) => compareText(a.Name, b.Name)),
    };
  }, [query, entries, categories]);

  const rows = [
    ...(segment === 'categories' ? [] : words.map((entry) => ({ type: 'word', entry }))),
    ...(segment === 'words'
      ? []
      : matchedCategories.map((category) => ({ type: 'category', category }))),
  ];

  const counts = {
    all: words.length + matchedCategories.length,
    words: words.length,
    categories: matchedCategories.length,
  };

  return (
    <View style={styles.container}>
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
          onChangeText={setQuery}
          placeholder="Mot, définition, catégorie…"
          placeholderTextColor={colors.textMuted}
          autoCapitalize="none"
          autoCorrect={false}
          autoFocus
        />
        {query.length > 0 && (
          <Pressable onPress={() => setQuery('')} hitSlop={8}>
            <CategoryIcon iconKey="Phosphor.x" color={colors.textMuted} size={15} />
          </Pressable>
        )}
      </View>

      <View style={styles.segments}>
        {SEGMENTS.map((option) => {
          const isActive = option.key === segment;
          return (
            <Pressable
              key={option.key}
              onPress={() => setSegment(option.key)}
              style={[
                styles.segment,
                {
                  backgroundColor: isActive ? colors.accent : colors.chipBackground,
                },
              ]}
            >
              <Text
                style={[
                  styles.segmentText,
                  { color: isActive ? colors.textOnAccent : colors.chipForeground },
                ]}
              >
                {option.label} ({counts[option.key]})
              </Text>
            </Pressable>
          );
        })}
      </View>

      <FlatList
        data={rows}
        keyExtractor={(row) => (row.type === 'word' ? `w${row.entry.Id}` : `c${row.category.Id}`)}
        contentContainerStyle={styles.list}
        keyboardShouldPersistTaps="handled"
        ListEmptyComponent={
          <Text style={[styles.empty, { color: colors.textSecondary }]}>
            {query.trim().length === 0 ? 'Saisis un terme à rechercher.' : 'Aucun résultat.'}
          </Text>
        }
        renderItem={({ item }) => {
          if (item.type === 'category') {
            const category = categoryIndex.get(item.category.Id);
            return (
              <Pressable
                style={[styles.row, { borderBottomColor: colors.borderSubtle }]}
                onPress={() => {
                  setCategoryFilter({ kind: 'category', categoryId: item.category.Id });
                  router.push('/');
                }}
              >
                <CategoryIcon
                  iconKey={category?.icon}
                  color={category?.color ?? colors.iconNeutral}
                  size={20}
                />
                <View style={styles.rowText}>
                  <Text style={[styles.title, { color: colors.textPrimary }]}>
                    {item.category.Name}
                  </Text>
                  <Text style={[styles.subtitle, { color: colors.textMuted }]}>Catégorie</Text>
                </View>
              </Pressable>
            );
          }

          const lead = categoryIndex.get(item.entry.CategoryIds[0]);
          return (
            <Pressable
              style={[styles.row, { borderBottomColor: colors.borderSubtle }]}
              onPress={() => router.push(`/entry/${item.entry.Id}`)}
            >
              <CategoryIcon
                iconKey={lead?.icon}
                color={lead?.color ?? colors.iconNeutral}
                size={20}
              />
              <View style={styles.rowText}>
                <Text style={[styles.title, { color: colors.textPrimary }]}>{item.entry.Word}</Text>
                <Text style={[styles.subtitle, { color: colors.textSecondary }]} numberOfLines={1}>
                  {item.entry.Definition[0]}
                </Text>
              </View>
            </Pressable>
          );
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  searchField: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    height: 42,
    marginHorizontal: 14,
    marginTop: 10,
  },
  searchInput: { flex: 1, fontSize: 15, paddingVertical: 0 },
  segments: { flexDirection: 'row', gap: 8, paddingHorizontal: 14, paddingVertical: 10 },
  segment: { paddingHorizontal: 12, paddingVertical: 6, borderRadius: 999 },
  segmentText: { fontSize: 12, fontWeight: '600' },
  list: { paddingBottom: 16 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  rowText: { flex: 1 },
  title: { fontSize: 16, fontWeight: '600' },
  subtitle: { fontSize: 12, marginTop: 2 },
  empty: { padding: 28, textAlign: 'center' },
});
