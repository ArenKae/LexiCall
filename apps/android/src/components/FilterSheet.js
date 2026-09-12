import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { SortControl } from './SortControl';
import { useTheme } from '../theme/useTheme';
import { ALL_ENTRIES } from '../utils/filterEntries';

// Sheet behind the search bar's filter button: sort mode, plus the active
// category filter with a way back to "Toutes les entrées". Picking a specific
// category is the Catégories tab's job, not duplicated here.
export function FilterSheet({
  visible,
  onClose,
  sortMode,
  onSortModeChange,
  filter,
  filterLabel,
  activeCategory,
  onResetFilter,
}) {
  const colors = useTheme();

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable style={[styles.sheet, { backgroundColor: colors.surface }]}>
          <Text style={[styles.sectionTitle, { color: colors.textSecondary }]}>Trier par</Text>
          <SortControl value={sortMode} onChange={onSortModeChange} />

          <Text style={[styles.sectionTitle, { color: colors.textSecondary }]}>Catégorie</Text>
          <View style={styles.activeFilter}>
            {activeCategory && <View style={[styles.dot, { backgroundColor: activeCategory.color }]} />}
            <Text style={[styles.activeFilterLabel, { color: colors.textPrimary }]}>{filterLabel}</Text>
          </View>
          {filter.kind !== ALL_ENTRIES && (
            <Pressable
              style={[styles.reset, { backgroundColor: colors.chipBackground }]}
              onPress={onResetFilter}
            >
              <CategoryIcon iconKey="Phosphor.x" color={colors.chipForeground} size={13} />
              <Text style={{ color: colors.chipForeground, fontSize: 13, fontWeight: '600' }}>
                Toutes les entrées
              </Text>
            </Pressable>
          )}

          <Pressable
            style={[styles.close, { backgroundColor: colors.accent }]}
            onPress={onClose}
          >
            <Text style={{ color: colors.textOnAccent, fontWeight: '700' }}>Fermer</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0, 0, 0, 0.5)' },
  sheet: { borderTopLeftRadius: 18, borderTopRightRadius: 18, padding: 16, paddingBottom: 28 },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.4,
    marginBottom: 8,
    marginTop: 14,
  },
  activeFilter: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  dot: { width: 8, height: 8, borderRadius: 999 },
  activeFilterLabel: { fontSize: 15 },
  reset: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    alignSelf: 'flex-start',
    marginTop: 10,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  close: { marginTop: 20, borderRadius: 12, paddingVertical: 13, alignItems: 'center' },
});
