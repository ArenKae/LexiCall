import { Modal, Pressable, StyleSheet, Text } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { SortControl } from './SortControl';
import { useTheme } from '../theme/useTheme';

// Sheet behind the search bar's filter button: sort mode, plus the active
// category filter with a way back to "Toutes les entrées". Picking a specific
// category is the Catégories tab's job, not duplicated here.
export function FilterSheet({ visible, onClose, sortMode, onSortModeChange }) {
  const colors = useTheme();
  const insets = useSafeAreaInsets();

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable
          style={[
            styles.sheet,
            { backgroundColor: colors.surface, paddingBottom: 28 + insets.bottom },
          ]}
        >
          <Text style={[styles.sectionTitle, { color: colors.textSecondary }]}>Trier par</Text>
          <SortControl value={sortMode} onChange={onSortModeChange} />

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
  close: { marginTop: 20, borderRadius: 12, paddingVertical: 13, alignItems: 'center' },
});
