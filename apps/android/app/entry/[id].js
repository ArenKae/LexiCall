import { useLocalSearchParams, useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';

const UNDEFINED_TYPE = 'Undefined';

function Section({ title, iconKey, color, children }) {
  return (
    <View style={styles.section}>
      <View style={styles.sectionHeader}>
        <CategoryIcon iconKey={iconKey} color={color} size={16} />
        <Text style={[styles.sectionTitle, { color }]}>{title}</Text>
      </View>
      {children}
    </View>
  );
}

// Full read-only view of one entry. Images are metadata-only until the fetch
// route is wired up, so nothing is drawn for them yet.
export default function EntryDetail() {
  const { id } = useLocalSearchParams();
  const colors = useTheme();
  const router = useRouter();
  const categoryIndex = useCategoryIndex();
  const entry = useVocabularyStore((state) => state.entries.find((item) => item.Id === id));
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);

  if (!entry) {
    return (
      <View style={styles.centered}>
        <Text style={{ color: colors.textSecondary }}>Entrée introuvable.</Text>
      </View>
    );
  }

  const types = entry.Type.filter((type) => type !== UNDEFINED_TYPE);
  const categories = entry.CategoryIds.map((categoryId) => categoryIndex.get(categoryId)).filter(
    Boolean
  );

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text selectable style={[styles.word, { color: colors.textPrimary }]}>
        {entry.Word}
      </Text>
      {types.length > 0 && (
        <Text selectable style={[styles.type, { color: colors.textSecondary }]}>
          {types.join(', ')}
        </Text>
      )}

      {entry.IsArchived && (
        <View style={[styles.banner, { backgroundColor: colors.surfaceHover }]}>
          <View style={styles.bannerHeader}>
            <CategoryIcon iconKey="Solar.archive-check" color={colors.warning} size={16} />
            <Text style={[styles.bannerTitle, { color: colors.warning }]}>Entrée archivée</Text>
          </View>
          <Text style={[styles.bannerText, { color: colors.textSecondary }]}>
            Masquée de « Toutes les entrées », toujours visible dans ses catégories.
          </Text>
        </View>
      )}

      {categories.length > 0 && (
        <View style={styles.chips}>
          {categories.map((category) => (
            <Pressable
              key={category.Id}
              style={[styles.chip, { backgroundColor: colors.chipBackground }]}
              onPress={() => {
                setCategoryFilter({ kind: 'category', categoryId: category.Id });
                router.push('/');
              }}
            >
              <CategoryIcon iconKey={category.icon} color={category.color} size={14} />
              <Text style={[styles.chipText, { color: colors.chipForeground }]}>
                {category.Name}
              </Text>
            </Pressable>
          ))}
        </View>
      )}

      {entry.Definition.map((sense, index) => (
        <View key={`${index}-${sense.slice(0, 12)}`} style={styles.sense}>
          {entry.Definition.length > 1 && (
            <Text style={[styles.senseNumber, { color: colors.textSecondary }]}>{index + 1}.</Text>
          )}
          <Text selectable style={[styles.senseText, { color: colors.textPrimary }]}>
            {sense}
          </Text>
        </View>
      ))}

      {entry.Synonyms.length > 0 && (
        <Section title="Synonymes" iconKey="Phosphor.book-open" color={colors.textSecondary}>
          <View style={styles.chips}>
            {entry.Synonyms.map((synonym) => (
              <View
                key={synonym}
                style={[styles.chip, { backgroundColor: colors.chipSynonymBackground }]}
              >
                <Text selectable style={[styles.chipText, { color: colors.chipForeground }]}>
                  {synonym}
                </Text>
              </View>
            ))}
          </View>
        </Section>
      )}

      {entry.ExampleSentences.length > 0 && (
        <Section title="Exemples" iconKey="Phosphor.quotes" color={colors.textSecondary}>
          {entry.ExampleSentences.map((example) => (
            <Text key={example} selectable style={[styles.body, { color: colors.textSecondary }]}>
              {example}
            </Text>
          ))}
        </Section>
      )}

      {entry.Notes.length > 0 && (
        <Section title="Notes personnelles" iconKey="Phosphor.note-pencil" color={colors.textSecondary}>
          <Text selectable style={[styles.body, { color: colors.textSecondary }]}>
            {entry.Notes}
          </Text>
        </Section>
      )}

      {entry.Source.length > 0 && (
        <Section title="Source" iconKey="Solar.document" color={colors.textSecondary}>
          <Text selectable style={[styles.body, { color: colors.textSecondary }]}>
            {entry.Source}
          </Text>
        </Section>
      )}

      {entry.Images.length > 0 && (
        <Text style={[styles.imageNote, { color: colors.textMuted }]}>
          {entry.Images.length} image(s) attachée(s) — affichage à venir.
        </Text>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: { padding: 18, paddingBottom: 40 },
  centered: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  // Serif for the word and its senses, as on the desktop client.
  word: { fontFamily: 'serif', fontSize: 30 },
  type: { fontSize: 14, fontStyle: 'italic', marginTop: 2 },
  banner: { borderRadius: 10, padding: 10, marginTop: 12, gap: 4 },
  bannerHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  bannerTitle: { fontSize: 13, fontWeight: '600' },
  bannerText: { fontSize: 12, lineHeight: 17 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 12 },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 999,
  },
  chipText: { fontSize: 12 },
  sense: { flexDirection: 'row', gap: 8, marginTop: 14 },
  senseNumber: { fontFamily: 'serif', fontSize: 16 },
  senseText: { flex: 1, fontFamily: 'serif', fontSize: 16, lineHeight: 24 },
  section: { marginTop: 22 },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', gap: 6, marginBottom: 8 },
  sectionTitle: { fontSize: 12, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.5 },
  body: { fontSize: 14, lineHeight: 20, marginBottom: 6 },
  imageNote: { fontSize: 12, marginTop: 24, fontStyle: 'italic' },
});
