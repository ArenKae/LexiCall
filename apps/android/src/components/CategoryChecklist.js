import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useCategoryIndex } from '../hooks/useCategoryIndex';
import { useTheme } from '../theme/useTheme';
import { flattenCategories } from '../utils/categoryHierarchy';
import { useVocabularyStore } from '../store/useVocabularyStore';

const INDENT = 16;

// Flat, indented checklist of every category. Categories are optional: an
// empty selection is valid.
export function CategoryChecklist({ selectedIds, onChange }) {
  const colors = useTheme();
  const categories = useVocabularyStore((state) => state.categories);
  const categoryIndex = useCategoryIndex();
  const rows = flattenCategories(categories);

  const toggle = (id) => {
    onChange(
      selectedIds.includes(id) ? selectedIds.filter((item) => item !== id) : [...selectedIds, id]
    );
  };

  if (rows.length === 0) {
    return (
      <Text style={{ color: colors.textMuted, fontSize: 13 }}>Aucune catégorie créée pour le moment.</Text>
    );
  }

  return (
    <View style={styles.list}>
      {rows.map(({ category, depth }) => {
        const isSelected = selectedIds.includes(category.Id);
        const resolved = categoryIndex.get(category.Id);

        return (
          <Pressable
            key={category.Id}
            onPress={() => toggle(category.Id)}
            style={[styles.row, { marginLeft: depth * INDENT }]}
          >
            <View
              style={[
                styles.checkbox,
                {
                  borderColor: isSelected ? colors.accent : colors.borderStrong,
                  backgroundColor: isSelected ? colors.accent : 'transparent',
                },
              ]}
            >
              {isSelected && <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={12} />}
            </View>
            <CategoryIcon iconKey={resolved?.icon} color={resolved?.color ?? colors.iconNeutral} size={17} />
            <Text style={[styles.label, { color: colors.textPrimary }]} numberOfLines={1}>
              {category.Name}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  list: { gap: 2 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 7 },
  checkbox: {
    width: 18,
    height: 18,
    borderRadius: 5,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  label: { flex: 1, fontSize: 14 },
});
