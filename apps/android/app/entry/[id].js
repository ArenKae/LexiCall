import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategorizationReviewModal } from '../../src/components/CategorizationReviewModal';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { EnrichmentReviewModal } from '../../src/components/EnrichmentReviewModal';
import { EntryImageGallery } from '../../src/components/EntryImageGallery';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { useEntryImages } from '../../src/hooks/useEntryImages';
import { createApiClient } from '../../src/services/apiClient';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { UNDEFINED_TYPE } from '../../src/utils/vocabularyEntryTypes';

// Plain frontend autocorrect, unrelated to the LLM call itself — just a
// courtesy fix-up applied alongside a successful enrichment response.
function capitalizeFirstLetter(word) {
  return word.length > 0 && !/[A-ZÀ-Ü]/.test(word[0]) ? word[0].toUpperCase() + word.slice(1) : word;
}

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

function ActionButton({ iconKey, label, color, onPress }) {
  return (
    <Pressable style={styles.actionButton} onPress={onPress}>
      <CategoryIcon iconKey={iconKey} color={color} size={22} />
      <Text style={[styles.actionLabel, { color }]} numberOfLines={1}>
        {label}
      </Text>
    </Pressable>
  );
}

// Full read-only view of one entry.
export default function EntryDetail() {
  const { id } = useLocalSearchParams();
  const colors = useTheme();
  const router = useRouter();
  const categoryIndex = useCategoryIndex();
  const entry = useVocabularyStore((state) => state.entries.find((item) => item.Id === id));
  const apiBaseUrl = useVocabularyStore((state) => state.apiBaseUrl);
  const apiKey = useVocabularyStore((state) => state.apiKey);
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);
  const toggleArchive = useVocabularyStore((state) => state.toggleArchive);
  const deleteEntry = useVocabularyStore((state) => state.deleteEntry);
  const updateEntry = useVocabularyStore((state) => state.updateEntry);
  const addCategory = useVocabularyStore((state) => state.addCategory);
  // Called before the missing-entry branch below: hooks can't sit after a return.
  const { states: imageStates, retry: retryImage } = useEntryImages(entry);
  const insets = useSafeAreaInsets();
  const [isEnriching, setIsEnriching] = useState(false);
  const [enrichmentSuggestions, setEnrichmentSuggestions] = useState(null);
  const [isCategorizing, setIsCategorizing] = useState(false);
  const [categorizationSuggestions, setCategorizationSuggestions] = useState(null);
  const apiClient = createApiClient(apiBaseUrl, apiKey);

  if (!entry) {
    return (
      <View style={[styles.centered, { paddingTop: insets.top, paddingBottom: insets.bottom }]}>
        <Text style={{ color: colors.textSecondary }}>Entrée introuvable.</Text>
        <Pressable style={styles.missingBack} onPress={() => router.back()} hitSlop={10}>
          <CategoryIcon iconKey="Phosphor.caret-left" color={colors.accent} size={18} />
          <Text style={{ color: colors.accent, fontSize: 15, fontWeight: '600' }}>Retour</Text>
        </Pressable>
      </View>
    );
  }

  const types = entry.Type.filter((type) => type !== UNDEFINED_TYPE);
  const categories = entry.CategoryIds.map((categoryId) => categoryIndex.get(categoryId)).filter(
    Boolean
  );

  async function handleEnrich() {
    setIsEnriching(true);
    const { status, result, errorDetail } = await apiClient.suggestFields({
      Word: entry.Word,
      Definition: entry.Definition,
      Type: entry.Type,
      Synonyms: entry.Synonyms,
      ExampleSentences: entry.ExampleSentences,
      LockedFields: entry.LockedFields,
    });
    setIsEnriching(false);

    if (status === 'NotConfigured') {
      Alert.alert('Enrichissement IA', 'Nécessite une synchronisation API configurée (voir Options).');
      return;
    }
    if (status !== 'Ok' || !result) {
      Alert.alert(
        'Enrichissement IA',
        errorDetail ? `Impossible d’obtenir des suggestions : ${errorDetail}` : 'Impossible d’obtenir des suggestions pour le moment. Réessaie plus tard.'
      );
      return;
    }
    if (!result.word_recognized) {
      Alert.alert(
        'Enrichissement IA',
        `« ${entry.Word} » n’a pas été reconnu comme un mot ou une expression française existante — aucune suggestion n’a pu être générée.`
      );
      return;
    }
    if (!result.definition && !result.type && !result.synonyms && !result.example_sentences) {
      Alert.alert('Enrichissement IA', 'Aucune suggestion : tous les champs sont verrouillés ou déjà jugés satisfaisants.');
      return;
    }

    setEnrichmentSuggestions(result);
  }

  function handleSaveEnrichment(result) {
    setEnrichmentSuggestions(null);
    updateEntry(entry.Id, {
      Word: capitalizeFirstLetter(entry.Word),
      Definition: result.Definition ?? entry.Definition,
      Type: result.Type ?? entry.Type,
      Synonyms: result.Synonyms ?? entry.Synonyms,
      ExampleSentences: result.ExampleSentences ?? entry.ExampleSentences,
      Notes: entry.Notes,
      Source: entry.Source,
      CategoryIds: entry.CategoryIds,
      IsArchived: entry.IsArchived,
      LockedFields: entry.LockedFields,
      Images: entry.Images,
    });
  }

  async function handleCategorize() {
    setIsCategorizing(true);
    const { status, result, errorDetail } = await apiClient.categorize(entry.Word, entry.Definition);
    setIsCategorizing(false);

    if (status === 'NotConfigured') {
      Alert.alert('Catégorisation IA', 'Nécessite une synchronisation API configurée (voir Options).');
      return;
    }
    if (status !== 'Ok' || !result) {
      Alert.alert(
        'Catégorisation IA',
        errorDetail ? `Impossible d’obtenir une suggestion : ${errorDetail}` : 'Impossible d’obtenir une suggestion de catégorie pour le moment. Réessaie plus tard.'
      );
      return;
    }
    if (!result.word_recognized) {
      Alert.alert(
        'Catégorisation IA',
        `« ${entry.Word} » n’a pas été reconnu comme un mot ou une expression française existante — aucune catégorie n’a été proposée.`
      );
      return;
    }
    if (result.suggestions.length === 0) {
      Alert.alert('Catégorisation IA', 'Aucune suggestion de catégorie pour cette entrée.');
      return;
    }

    setCategorizationSuggestions(result.suggestions);
  }

  function handleSaveCategorization(results) {
    setCategorizationSuggestions(null);

    const categoryIds = [...entry.CategoryIds];

    for (const result of results) {
      let categoryIdToAttach = result.existingCategoryId;

      if (result.newCategoryName) {
        const { error, category: newCategory } = addCategory({
          Name: result.newCategoryName,
          ParentId: result.newCategoryParentId,
          Description: result.newCategoryDescription ?? '',
          IconGlyph: result.newCategoryIconGlyph ?? '',
        });

        if (error) {
          Alert.alert('Création de catégorie impossible', error);
          continue;
        }
        categoryIdToAttach = newCategory.Id;
      }

      if (categoryIdToAttach && !categoryIds.includes(categoryIdToAttach)) {
        categoryIds.push(categoryIdToAttach);
      }
    }

    if (categoryIds.length === entry.CategoryIds.length) {
      return;
    }

    updateEntry(entry.Id, {
      Word: entry.Word,
      Definition: entry.Definition,
      Type: entry.Type,
      Synonyms: entry.Synonyms,
      ExampleSentences: entry.ExampleSentences,
      Notes: entry.Notes,
      Source: entry.Source,
      CategoryIds: categoryIds,
      IsArchived: entry.IsArchived,
      LockedFields: entry.LockedFields,
      Images: entry.Images,
    });
  }

  function confirmDelete() {
    Alert.alert('Confirmer la suppression', `Supprimer « ${entry.Word} » ?`, [
      { text: 'Annuler', style: 'cancel' },
      {
        text: 'Supprimer',
        style: 'destructive',
        onPress: () => {
          deleteEntry(entry.Id);
          router.back();
        },
      },
    ]);
  }

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={[styles.content, { paddingTop: insets.top + 18 }]}>
      <Text selectable style={[styles.word, { color: colors.textPrimary }]}>
        {entry.Word}
      </Text>
      {types.length > 0 && (
        <Text selectable style={[styles.type, { color: colors.textSecondary }]}>
          {types.join(', ')}
        </Text>
      )}

      {apiClient.isConfigured() && (
        <View style={styles.aiButtons}>
          <Pressable
            style={[styles.aiButton, { borderColor: colors.borderStrong, opacity: isEnriching ? 0.6 : 1 }]}
            onPress={handleEnrich}
            disabled={isEnriching}
          >
            {isEnriching ? (
              <ActivityIndicator size="small" color={colors.textSecondary} />
            ) : (
              <CategoryIcon iconKey="Phosphor.sparkle" color={colors.textSecondary} size={14} />
            )}
            <Text style={{ color: colors.textPrimary, fontSize: 13, fontWeight: '600' }}>Enrichir</Text>
          </Pressable>
          <Pressable
            style={[styles.aiButton, { borderColor: colors.borderStrong, opacity: isCategorizing ? 0.6 : 1 }]}
            onPress={handleCategorize}
            disabled={isCategorizing}
          >
            {isCategorizing ? (
              <ActivityIndicator size="small" color={colors.textSecondary} />
            ) : (
              <CategoryIcon iconKey="Solar.tag" color={colors.textSecondary} size={14} />
            )}
            <Text style={{ color: colors.textPrimary, fontSize: 13, fontWeight: '600' }}>Catégoriser</Text>
          </Pressable>
        </View>
      )}

      {enrichmentSuggestions && (
        <EnrichmentReviewModal
          visible
          word={entry.Word}
          currentDefinition={entry.Definition}
          currentType={entry.Type}
          currentSynonyms={entry.Synonyms}
          currentExampleSentences={entry.ExampleSentences}
          suggestions={enrichmentSuggestions}
          apiClient={apiClient}
          onClose={() => setEnrichmentSuggestions(null)}
          onSave={handleSaveEnrichment}
        />
      )}

      {categorizationSuggestions && (
        <CategorizationReviewModal
          visible
          suggestions={categorizationSuggestions}
          currentCategoryNames={categories.map((category) => category.Name)}
          onClose={() => setCategorizationSuggestions(null)}
          onSave={handleSaveCategorization}
        />
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
        <Section title="Images" iconKey="Phosphor.image" color={colors.textSecondary}>
          <EntryImageGallery images={entry.Images} states={imageStates} onRetry={retryImage} />
        </Section>
      )}
      </ScrollView>

      <View
        style={[
          styles.actionBar,
          {
            backgroundColor: colors.surface,
            borderTopColor: colors.borderSubtle,
            paddingBottom: 8 + insets.bottom,
          },
        ]}
      >
        <ActionButton
          iconKey="Phosphor.arrow-left"
          label="Retour"
          color={colors.textPrimary}
          onPress={() => router.back()}
        />
        <ActionButton
          iconKey="Phosphor.pencil"
          label="Modifier"
          color={colors.textPrimary}
          onPress={() => router.push(`/entry/edit?id=${entry.Id}`)}
        />
        <ActionButton
          iconKey="Solar.archive-check"
          label={entry.IsArchived ? 'Désarchiver' : 'Archiver'}
          color={colors.textPrimary}
          onPress={() => toggleArchive(entry.Id)}
        />
        <ActionButton
          iconKey="Phosphor.trash"
          label="Supprimer"
          color={colors.danger}
          onPress={confirmDelete}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  content: { padding: 18, paddingBottom: 28 },
  centered: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 16 },
  missingBack: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingVertical: 8 },
  actionBar: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    borderTopWidth: 1,
    paddingTop: 8,
    paddingHorizontal: 4,
  },
  actionButton: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    minHeight: 48,
    paddingHorizontal: 2,
    paddingVertical: 6,
  },
  actionLabel: { fontSize: 11, fontWeight: '600' },
  // Serif for the word and its senses.
  word: { fontFamily: 'serif', fontSize: 30 },
  type: { fontSize: 14, fontStyle: 'italic', marginTop: 2 },
  aiButtons: { flexDirection: 'row', gap: 8, marginTop: 14 },
  aiButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 7,
  },
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
});
