import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';
import { useVocabularyStore } from '../store/useVocabularyStore';
import { flattenCategories, getDescendantIds } from '../utils/categoryHierarchy';

const ROOT_LABEL = '(Aucune — catégorie racine)';
const INDENT = 16;

// Closed, shows the selected parent's name (or the root label); open, lists
// every other category indented by depth. excludeCategoryId (the category
// being edited) and its whole subtree are left out, so a category can never
// become its own ancestor.
export function CategoryParentPicker({ selectedId, excludeCategoryId, onChange }) {
  const colors = useTheme();
  const categories = useVocabularyStore((state) => state.categories);
  const categoryOrder = useVocabularyStore((state) => state.categoryOrder);
  const [open, setOpen] = useState(false);

  const excludedIds = excludeCategoryId
    ? new Set([excludeCategoryId, ...getDescendantIds(categories, excludeCategoryId)])
    : new Set();
  const options = flattenCategories(categories, categoryOrder).filter(
    ({ category }) => !excludedIds.has(category.Id)
  );

  const selected = categories.find((category) => category.Id === selectedId);

  function pick(id) {
    onChange(id);
    setOpen(false);
  }

  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        style={[styles.field, { borderColor: colors.borderSubtle, backgroundColor: colors.surface }]}
      >
        <Text style={[styles.summary, { color: colors.textPrimary }]} numberOfLines={1}>
          {selected?.Name ?? ROOT_LABEL}
        </Text>
        <CategoryIcon iconKey="Phosphor.caret-down" color={colors.textMuted} size={14} />
      </Pressable>

      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)}>
          <Pressable style={[styles.sheet, { backgroundColor: colors.surface }]}>
            <ScrollView style={styles.list}>
              <Pressable style={styles.row} onPress={() => pick(null)}>
                <Text style={[styles.rowLabel, { color: colors.textPrimary }]}>{ROOT_LABEL}</Text>
                {selectedId == null && (
                  <CategoryIcon iconKey="Phosphor.check" color={colors.accent} size={16} />
                )}
              </Pressable>

              {options.map(({ category, depth }) => (
                <Pressable
                  key={category.Id}
                  style={[styles.row, { marginLeft: depth * INDENT }]}
                  onPress={() => pick(category.Id)}
                >
                  <Text style={[styles.rowLabel, { color: colors.textPrimary }]} numberOfLines={1}>
                    {category.Name}
                  </Text>
                  {selectedId === category.Id && (
                    <CategoryIcon iconKey="Phosphor.check" color={colors.accent} size={16} />
                  )}
                </Pressable>
              ))}
            </ScrollView>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  field: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  summary: { flex: 1, fontSize: 15 },
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0, 0, 0, 0.5)' },
  sheet: { borderTopLeftRadius: 18, borderTopRightRadius: 18, padding: 16, paddingBottom: 28 },
  list: { maxHeight: 420 },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 11 },
  rowLabel: { fontSize: 15, flex: 1 },
});
