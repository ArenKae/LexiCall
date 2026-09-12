import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, View } from 'react-native';
import { useTheme } from '../theme/useTheme';
import { useVocabularyStore } from '../store/useVocabularyStore';

// Header dot: colour encodes the sync state, tapping opens the history. Hidden
// entirely while no API is configured, since there is nothing to report then.
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
      <View style={[styles.dot, { backgroundColor: fill }]} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  hit: { paddingHorizontal: 14, paddingVertical: 8 },
  dot: { width: 11, height: 11, borderRadius: 999 },
});
