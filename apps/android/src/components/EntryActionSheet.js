import { Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

function Row({ iconKey, label, color, onPress }) {
  const colors = useTheme();
  return (
    <Pressable style={styles.row} onPress={onPress}>
      <CategoryIcon iconKey={iconKey} color={color ?? colors.textPrimary} size={20} />
      <Text style={[styles.rowText, { color: color ?? colors.textPrimary }]}>{label}</Text>
    </Pressable>
  );
}

// Bottom sheet for an entry's actions. Dupliquer/Enrichir avec l'IA from the
// mockup aren't included: neither exists yet on any client.
export function EntryActionSheet({ visible, entry, onClose, onEdit, onToggleArchive, onDelete }) {
  const colors = useTheme();

  function confirmDelete() {
    onClose();
    Alert.alert('Confirmer la suppression', `Supprimer « ${entry.Word} » ?`, [
      { text: 'Annuler', style: 'cancel' },
      { text: 'Supprimer', style: 'destructive', onPress: onDelete },
    ]);
  }

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable style={[styles.sheet, { backgroundColor: colors.surface }]}>
          <Text style={[styles.title, { color: colors.textPrimary }]}>Que souhaitez-vous faire ?</Text>

          <Row iconKey="Phosphor.pencil" label="Modifier l’entrée" onPress={onEdit} />
          <Row
            iconKey="Solar.archive-check"
            label={entry.IsArchived ? 'Désarchiver' : 'Archiver'}
            onPress={onToggleArchive}
          />
          <Row iconKey="Phosphor.trash" label="Supprimer" color={colors.danger} onPress={confirmDelete} />

          <Pressable
            style={[styles.cancel, { backgroundColor: colors.chipBackground }]}
            onPress={onClose}
          >
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
