import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../src/theme/useTheme';
import { useVocabularyStore } from '../src/store/useVocabularyStore';

// Home screen.
export default function Home() {
  const colors = useTheme();
  const entryCount = useVocabularyStore((state) => state.entries.length);

  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      <Text style={[styles.title, { color: colors.textPrimary }]}>LexiCall</Text>
      <Text style={[styles.subtitle, { color: colors.textSecondary }]}>
        {entryCount} entrée(s) chargée(s)
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  title: {
    fontSize: 24,
    fontWeight: '600',
  },
  subtitle: {
    fontSize: 14,
  },
});
