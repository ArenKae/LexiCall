import { Alert, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from '../src/components/CategoryIcon';
import { useTheme } from '../src/theme/useTheme';
import { useVocabularyStore } from '../src/store/useVocabularyStore';

// Icon shape encodes the operation, fill colour the outcome — plus a written
// " · Échec" suffix, since colour alone is easy to miss in a dense list.
const OPERATION_ICONS = {
  Push: 'Phosphor.caret-circle-up',
  Pull: 'Phosphor.caret-circle-down',
  Delete: 'Phosphor.trash',
};

const OPERATION_LABELS = { Push: 'Envoyé', Pull: 'Reçu', Delete: 'Supprimé' };
const CHANGE_LABELS = { Created: 'création', Updated: 'modification' };

function formatTimestamp(iso) {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleString('fr-FR', {
        day: '2-digit',
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      });
}

export default function SyncHistory() {
  const colors = useTheme();
  const syncHistory = useVocabularyStore((state) => state.syncHistory);
  const clearSyncHistory = useVocabularyStore((state) => state.clearSyncHistory);

  function confirmClear() {
    Alert.alert('Vider l’historique', 'Les lignes affichées seront supprimées.', [
      { text: 'Annuler', style: 'cancel' },
      { text: 'Vider', style: 'destructive', onPress: clearSyncHistory },
    ]);
  }

  return (
    <View style={styles.container}>
      <FlatList
        data={syncHistory}
        keyExtractor={(row) => row.Id}
        contentContainerStyle={styles.list}
        ListEmptyComponent={
          <Text style={[styles.empty, { color: colors.textSecondary }]}>
            Aucune opération de synchronisation enregistrée.
          </Text>
        }
        renderItem={({ item }) => {
          const failed = item.Outcome === 'Failure';
          const details = [
            OPERATION_LABELS[item.Operation] ?? item.Operation,
            item.ChangeKind ? CHANGE_LABELS[item.ChangeKind] : null,
            item.EntityType === 'Category' ? 'catégorie' : null,
            formatTimestamp(item.Timestamp),
          ].filter(Boolean);

          return (
            <View style={[styles.row, { borderBottomColor: colors.borderSubtle }]}>
              <CategoryIcon
                iconKey={OPERATION_ICONS[item.Operation]}
                color={failed ? colors.danger : colors.success}
                size={20}
              />
              <View style={styles.rowText}>
                <Text style={[styles.label, { color: colors.textPrimary }]} numberOfLines={1}>
                  {item.EntityLabel}
                  {failed && ' · Échec'}
                </Text>
                <Text style={[styles.details, { color: colors.textMuted }]}>
                  {details.join(' · ')}
                </Text>
              </View>
            </View>
          );
        }}
      />

      {syncHistory.length > 0 && (
        <View style={[styles.footer, { borderTopColor: colors.borderSubtle }]}>
          <Pressable style={[styles.clear, { borderColor: colors.danger }]} onPress={confirmClear}>
            <Text style={{ color: colors.danger, fontWeight: '600' }}>Vider l’historique</Text>
          </Pressable>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  list: { paddingBottom: 16 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  rowText: { flex: 1 },
  label: { fontSize: 15, fontWeight: '600' },
  details: { fontSize: 12, marginTop: 2 },
  empty: { padding: 28, textAlign: 'center' },
  footer: { borderTopWidth: 1, padding: 14 },
  clear: { borderWidth: 1, borderRadius: 10, paddingVertical: 12, alignItems: 'center' },
});
