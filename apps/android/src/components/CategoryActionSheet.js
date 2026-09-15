import { Alert, Modal, Pressable, StyleSheet, Text } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

function Row({ iconKey, label, color, disabled, onPress }) {
  const colors = useTheme();
  const tint = disabled ? colors.textMuted : (color ?? colors.textPrimary);

  return (
    <Pressable style={styles.row} onPress={disabled ? undefined : onPress} disabled={disabled}>
      <CategoryIcon iconKey={iconKey} color={tint} size={20} />
      <Text style={[styles.rowText, { color: tint }]}>{label}</Text>
    </Pressable>
  );
}

// Bottom sheet for a category's actions, opened by a long-press on its row
// in the Catégories tab.
export function CategoryActionSheet({
  visible,
  category,
  onClose,
  onAddSubcategory,
  onReorder,
  onEdit,
  onDelete,
}) {
  const colors = useTheme();
  const insets = useSafeAreaInsets();

  function confirmDelete() {
    onClose();
    Alert.alert('Confirmer la suppression', `Supprimer « ${category.Name} » ?`, [
      { text: 'Annuler', style: 'cancel' },
      { text: 'Supprimer', style: 'destructive', onPress: onDelete },
    ]);
  }

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable
          style={[
            styles.sheet,
            { backgroundColor: colors.surface, paddingBottom: 28 + insets.bottom },
          ]}
        >
          <Text style={[styles.title, { color: colors.textPrimary }]} numberOfLines={1}>
            {category.Name}
          </Text>

          <Row iconKey="Phosphor.plus" label="Nouvelle sous-catégorie" onPress={onAddSubcategory} />
          <Row iconKey="Phosphor.list-bullets" label="Réordonner" onPress={onReorder} />
          <Row iconKey="Phosphor.pencil" label="Modifier" onPress={onEdit} />
          <Row iconKey="Phosphor.trash" label="Supprimer" color={colors.danger} onPress={confirmDelete} />

          <Pressable style={[styles.cancel, { backgroundColor: colors.chipBackground }]} onPress={onClose}>
            <Text style={{ color: colors.textPrimary, fontWeight: '600' }}>Annuler</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0, 0, 0, 0.5)' },
  sheet: { borderTopLeftRadius: 18, borderTopRightRadius: 18, padding: 16, paddingBottom: 28, gap: 4 },
  title: { fontSize: 14, fontWeight: '600', textAlign: 'center', marginBottom: 10 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 14, paddingVertical: 13 },
  rowText: { fontSize: 15 },
  cancel: { marginTop: 10, borderRadius: 12, paddingVertical: 13, alignItems: 'center' },
});
