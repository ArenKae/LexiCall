import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../theme/useTheme';
import { useVocabularyStore } from '../store/useVocabularyStore';
import { SYNC_STATUS_LABELS } from '../utils/syncStatusLabels';

// Header label + dot: colour and text encode the sync state, tapping opens the
// history. Hidden entirely while no API is configured, since there is nothing
// to report then.
export function SyncStatusIndicator() {
  const colors = useTheme();
  const router = useRouter();
  const status = useVocabularyStore((state) => state.globalSyncStatus);

  if (status === 'NotConfigured') {
    return null;
  }

  const fill =
    status === 'Ok' ? colors.success : status === 'Syncing' ? colors.warning : colors.danger;

  return (
    <Pressable style={styles.hit} hitSlop={8} onPress={() => router.push('/sync-history')}>
      <Text style={[styles.label, { color: colors.textSecondary }]}>
        {SYNC_STATUS_LABELS[status] ?? status}
      </Text>
      <View style={[styles.dot, { backgroundColor: fill }]} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  hit: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 7,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  label: { fontSize: 13 },
  dot: { width: 11, height: 11, borderRadius: 999 },
});
