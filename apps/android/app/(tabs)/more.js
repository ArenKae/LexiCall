import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';

const STATUS_LABELS = {
  Ok: 'Connecté',
  Syncing: 'Synchronisation…',
  Problem: 'Erreur de synchronisation',
  NotConfigured: 'API non configurée',
};

function formatLastSynced(iso) {
  const date = iso ? new Date(iso) : null;

  return date && !Number.isNaN(date.getTime())
    ? `Dernière synchronisation le ${date.toLocaleString('fr-FR')}`
    : 'Pas encore synchronisé';
}

// Secondary destinations and a quick read on what the device holds locally.
export default function More() {
  const colors = useTheme();
  const router = useRouter();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const status = useVocabularyStore((state) => state.globalSyncStatus);
  const lastSyncedAt = useVocabularyStore((state) => state.lastSyncedAt);

  const statusColor =
    status === 'Ok'
      ? colors.success
      : status === 'Syncing'
        ? colors.warning
        : status === 'Problem'
          ? colors.danger
          : colors.textMuted;

  const rowStyle = [
    styles.row,
    { backgroundColor: colors.surface, borderColor: colors.borderSubtle },
  ];

  return (
    <View style={styles.container}>
      <Pressable style={rowStyle} onPress={() => router.push('/sync-history')}>
        <View style={[styles.dot, { backgroundColor: statusColor }]} />
        <View style={styles.rowText}>
          <Text style={[styles.label, { color: colors.textPrimary }]}>
            {STATUS_LABELS[status] ?? status}
          </Text>
          <Text style={[styles.info, { color: colors.textMuted }]}>
            {formatLastSynced(lastSyncedAt)}
          </Text>
        </View>
      </Pressable>

      <Pressable style={rowStyle} onPress={() => router.push('/options')}>
        <CategoryIcon iconKey="Phosphor.gear" color={colors.textSecondary} size={20} />
        <Text style={[styles.label, { color: colors.textPrimary }]}>Options</Text>
      </Pressable>

      <Text style={[styles.info, { color: colors.textMuted, paddingHorizontal: 4 }]}>
        {entries.length} entrée(s) · {categories.length} catégorie(s)
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 14, gap: 8 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 14,
  },
  dot: { width: 13, height: 13, borderRadius: 999, marginHorizontal: 3 },
  rowText: { flex: 1 },
  label: { flex: 1, fontSize: 15 },
  info: { fontSize: 12, marginTop: 2 },
});
