import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';
import { UNDEFINED_TYPE } from '../utils/vocabularyEntryTypes';

function typeLabel(types) {
  const real = types.filter((type) => type !== UNDEFINED_TYPE);
  return real.length > 0 ? real.join(', ') : '';
}

// One entry in the browsing list: word, grammatical type, first sense and the
// categories it belongs to.
export function EntryCard({ entry, categoryIndex, onPress }) {
  const colors = useTheme();
  const categories = entry.CategoryIds.map((id) => categoryIndex.get(id)).filter(Boolean);
  const leadCategory = categories[0];
  const type = typeLabel(entry.Type);

  return (
    <Pressable
      onPress={onPress}
      style={[styles.card, { backgroundColor: colors.surface, borderColor: colors.borderSubtle }]}
    >
      <View style={styles.headline}>
        {leadCategory && (
          <CategoryIcon iconKey={leadCategory.icon} color={leadCategory.color} size={22} />
        )}
        <View style={styles.headlineText}>
          <Text style={[styles.word, { color: colors.textPrimary }]}>{entry.Word}</Text>
          {type.length > 0 && (
            <Text style={[styles.type, { color: colors.textSecondary }]}>{type}</Text>
          )}
        </View>
        {entry.IsArchived && (
          <Text style={[styles.archived, { color: colors.textMuted }]}>archivée</Text>
        )}
      </View>

      <Text style={[styles.sense, { color: colors.textSecondary }]} numberOfLines={2}>
        {entry.Definition[0]}
      </Text>

      {categories.length > 0 && (
        <View style={styles.chips}>
          {categories.map((category) => (
            <View
              key={category.Id}
              style={[styles.chip, { backgroundColor: colors.chipBackground }]}
            >
              <View style={[styles.dot, { backgroundColor: category.color }]} />
              <Text style={[styles.chipText, { color: colors.textSecondary }]}>
                {category.Name}
              </Text>
            </View>
          ))}
        </View>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: { borderWidth: 1, borderRadius: 14, padding: 14, marginHorizontal: 14, marginBottom: 10 },
  headline: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  headlineText: { flex: 1 },
  word: { fontSize: 19, fontWeight: '700' },
  type: { fontSize: 12, fontStyle: 'italic', marginTop: 1 },
  archived: { fontSize: 11, fontStyle: 'italic' },
  sense: { fontSize: 13, lineHeight: 18, marginTop: 8 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 999,
  },
  dot: { width: 7, height: 7, borderRadius: 999 },
  chipText: { fontSize: 11 },
});
