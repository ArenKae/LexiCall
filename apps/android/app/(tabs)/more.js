import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';

// Secondary destinations and a quick read on what the device holds locally.
export default function More() {
  const colors = useTheme();
  const router = useRouter();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const lastPulledAt = useVocabularyStore((state) => state.lastPulledAt);

  return (
    <View style={styles.container}>
      <Pressable
        style={[styles.row, { backgroundColor: colors.surface, borderColor: colors.borderSubtle }]}
        onPress={() => router.push('/options')}
      >
        <CategoryIcon iconKey="Phosphor.gear" color={colors.textSecondary} size={20} />
        <Text style={[styles.label, { color: colors.textPrimary }]}>Options</Text>
      </Pressable>

      <Text style={[styles.info, { color: colors.textMuted }]}>
        {entries.length} entrée(s) · {categories.length} catégorie(s)
      </Text>
      <Text style={[styles.info, { color: colors.textMuted }]}>
        Dernière synchronisation : {lastPulledAt ?? 'jamais'}
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
  label: { flex: 1, fontSize: 15 },
  info: { fontSize: 12, paddingHorizontal: 4 },
});
