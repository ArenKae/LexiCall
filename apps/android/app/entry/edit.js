import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategorizationReviewModal } from '../../src/components/CategorizationReviewModal';
import { CategoryChecklist } from '../../src/components/CategoryChecklist';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { CollapsibleSection } from '../../src/components/CollapsibleSection';
import { EnrichmentReviewModal } from '../../src/components/EnrichmentReviewModal';
import { EntryImagePicker } from '../../src/components/EntryImagePicker';
import { LockToggle } from '../../src/components/LockToggle';
import { SenseListEditor } from '../../src/components/SenseListEditor';
import { TypeDropdown } from '../../src/components/TypeDropdown';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { createApiClient } from '../../src/services/apiClient';
import { useTheme } from '../../src/theme/useTheme';
import { createEntryDraft, useVocabularyStore } from '../../src/store/useVocabularyStore';
import {
  formatCommaSeparatedText,
  formatLineSeparatedText,
  parseCommaSeparatedText,
  parseLineSeparatedText,
} from '../../src/utils/textListParser';
import { UNDEFINED_TYPE } from '../../src/utils/vocabularyEntryTypes';

function fieldFromEntry(entry) {
  return {
    Word: entry.Word,
    Type: entry.Type,
    Definition: entry.Definition,
    CategoryIds: entry.CategoryIds,
    SynonymsText: formatCommaSeparatedText(entry.Synonyms),
    ExampleSentencesText: formatLineSeparatedText(entry.ExampleSentences),
    Notes: entry.Notes,
    Source: entry.Source,
    Images: entry.Images,
    IsArchived: entry.IsArchived,
    LockedFields: entry.LockedFields,
  };
}

function blankFields(initialCategoryId) {
  const draft = createEntryDraft(initialCategoryId);
  return {
    Word: draft.Word,
    Type: draft.Type,
    Definition: draft.Definition,
    CategoryIds: draft.CategoryIds,
    SynonymsText: '',
    ExampleSentencesText: '',
    Notes: draft.Notes,
    Source: draft.Source,
    Images: draft.Images,
    IsArchived: draft.IsArchived,
    LockedFields: draft.LockedFields,
  };
}

// Create/edit form for one entry. Only Word and at least one non-empty sense
// are required — everything else is optional, same as the field itself allows.
export default function EntryEditor() {
  const { id } = useLocalSearchParams();
  const colors = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const scrollRef = useRef(null);
  const scrollInnerRef = useRef(null);
  const scrollHeightRef = useRef(0);
  const existingEntry = useVocabularyStore((state) => state.entries.find((entry) => entry.Id === id));
  const categoryIndex = useCategoryIndex();
  const categoryFilter = useVocabularyStore((state) => state.categoryFilter);
  const apiBaseUrl = useVocabularyStore((state) => state.apiBaseUrl);
  const apiKey = useVocabularyStore((state) => state.apiKey);
  const addEntry = useVocabularyStore((state) => state.addEntry);
  const updateEntry = useVocabularyStore((state) => state.updateEntry);
  const addCategory = useVocabularyStore((state) => state.addCategory);
  const setEditorOpen = useVocabularyStore((state) => state.setEditorOpen);
  const apiClient = createApiClient(apiBaseUrl, apiKey);

  // Holds off the periodic resync: a pull landing mid-edit would swap the
  // record out from under the form.
  useEffect(() => {
    setEditorOpen(true);
    return () => setEditorOpen(false);
  }, [setEditorOpen]);

  const isEditing = existingEntry !== undefined;
  const [fields, setFields] = useState(() =>
    isEditing
      ? fieldFromEntry(existingEntry)
      : blankFields(categoryFilter.kind === 'category' ? categoryFilter.categoryId : null)
  );
  const [errorMessage, setErrorMessage] = useState('');
  const [isEnriching, setIsEnriching] = useState(false);
  const [enrichmentError, setEnrichmentError] = useState('');
  const [enrichmentSuggestions, setEnrichmentSuggestions] = useState(null);
  const [isCategorizing, setIsCategorizing] = useState(false);
  const [categorizationError, setCategorizationError] = useState('');
  const [categorizationSuggestions, setCategorizationSuggestions] = useState(null);

  const set = (key) => (value) => setFields((current) => ({ ...current, [key]: value }));
  const toggleLock = (field) => (locked) =>
    setFields((current) => ({
      ...current,
      LockedFields: locked
        ? [...current.LockedFields, field]
        : current.LockedFields.filter((item) => item !== field),
    }));

  // Opening the category list scrolls the entry's own category into the middle
  // of the form, which is otherwise buried under the whole tree.
  const centerOnRow = useCallback((rowRef) => {
    const inner = scrollInnerRef.current;

    if (!rowRef.current || !inner) {
      return;
    }

    rowRef.current.measureLayout(
      inner,
      (_x, y, _width, height) => {
        scrollRef.current?.scrollTo({
          y: Math.max(0, y - scrollHeightRef.current / 2 + height / 2),
          animated: true,
        });
      },
      () => {}
    );
  }, []);

  const canRunAi = fields.Word.trim().length > 0 && apiClient.isConfigured();
  // Disabled swaps the color instead of lowering opacity: a translucent accent
  // composites against the background into a visibly different blue.
  const aiColor = canRunAi ? colors.accent : colors.textMuted;

  async function handleEnrich() {
    if (!canRunAi) {
      return;
    }

    setEnrichmentError('');
    setIsEnriching(true);
    const { status, result, errorDetail } = await apiClient.suggestFields({
      Word: fields.Word.trim(),
      Definition: fields.Definition,
      Type: fields.Type,
      Synonyms: parseCommaSeparatedText(fields.SynonymsText),
      ExampleSentences: parseLineSeparatedText(fields.ExampleSentencesText),
      LockedFields: fields.LockedFields,
    });
    setIsEnriching(false);

    if (status === 'NotConfigured') {
      setEnrichmentError('L’enrichissement IA nécessite une synchronisation API configurée (voir Options).');
      return;
    }
    if (status !== 'Ok' || !result) {
      setEnrichmentError(
        errorDetail ? `Impossible d’obtenir des suggestions : ${errorDetail}` : 'Impossible d’obtenir des suggestions pour le moment. Réessaie plus tard.'
      );
      return;
    }
    if (!result.word_recognized) {
      Alert.alert(
        'Enrichissement IA',
        `« ${fields.Word.trim()} » n’a pas été reconnu comme un mot ou une expression française existante — aucune suggestion n’a pu été générée.`
      );
      return;
    }
    if (!result.definition && !result.type && !result.synonyms && !result.example_sentences) {
      Alert.alert('Enrichissement IA', 'Aucune suggestion : tous les champs sont verrouillés ou déjà jugés satisfaisants.');
      return;
    }

    // A word recognized by the model is a signal worth reflecting in the
    // form too, even though capitalization itself isn't an enrichment field.
    if (fields.Word.length > 0 && !/[A-ZÀ-Ü]/.test(fields.Word[0])) {
      set('Word')(fields.Word[0].toUpperCase() + fields.Word.slice(1));
    }
    setEnrichmentSuggestions(result);
  }

  function handleSaveEnrichment(result) {
    setEnrichmentSuggestions(null);
    setFields((current) => ({
      ...current,
      Definition: result.Definition ?? current.Definition,
      Type: result.Type ?? current.Type,
      SynonymsText: result.Synonyms ? formatCommaSeparatedText(result.Synonyms) : current.SynonymsText,
      ExampleSentencesText: result.ExampleSentences
        ? formatLineSeparatedText(result.ExampleSentences)
        : current.ExampleSentencesText,
    }));
  }

  async function handleCategorize() {
    if (!canRunAi) {
      return;
    }

    setCategorizationError('');
    setIsCategorizing(true);
    const { status, result, errorDetail } = await apiClient.categorize(fields.Word.trim(), fields.Definition);
    setIsCategorizing(false);

    if (status === 'NotConfigured') {
      setCategorizationError('La catégorisation nécessite une synchronisation API configurée (voir Options).');
      return;
    }
    if (status !== 'Ok' || !result) {
      setCategorizationError(
        errorDetail ? `Impossible d’obtenir une suggestion : ${errorDetail}` : 'Impossible d’obtenir une suggestion de catégorie pour le moment. Réessaie plus tard.'
      );
      return;
    }
    if (!result.word_recognized) {
      Alert.alert(
        'Catégorisation IA',
        `« ${fields.Word.trim()} » n’a pas été reconnu comme un mot ou une expression française existante — aucune catégorie n’a été proposée.`
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

    const categoryIds = [...fields.CategoryIds];

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

    set('CategoryIds')(categoryIds);
  }

  function handleSave() {
    const word = fields.Word.trim();
    const senses = fields.Definition.map((sense) => sense.trim()).filter((sense) => sense.length > 0);

    if (word.length === 0) {
      setErrorMessage('Le mot est obligatoire.');
      return;
    }
    if (senses.length === 0) {
      setErrorMessage('La définition est obligatoire.');
      return;
    }

    const payload = {
      Word: word,
      Type: fields.Type.length > 0 ? fields.Type : [UNDEFINED_TYPE],
      Definition: senses,
      CategoryIds: fields.CategoryIds,
      Synonyms: parseCommaSeparatedText(fields.SynonymsText),
      ExampleSentences: parseLineSeparatedText(fields.ExampleSentencesText),
      Notes: fields.Notes.trim(),
      Source: fields.Source.trim(),
      Images: fields.Images,
      IsArchived: fields.IsArchived,
      LockedFields: fields.LockedFields,
    };

    if (isEditing) {
      updateEntry(existingEntry.Id, payload);
      router.back();
    } else {
      addEntry(payload);
      router.dismissTo('/');
    }
  }

  const inputStyle = [
    styles.input,
    { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.surface },
  ];

  return (
    <>
      <Stack.Screen options={{ title: isEditing ? 'Modifier l’entrée' : 'Ajouter un mot' }} />
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          ref={scrollRef}
          innerViewRef={scrollInnerRef}
          onLayout={(event) => {
            scrollHeightRef.current = event.nativeEvent.layout.height;
          }}
          contentContainerStyle={styles.content}
        >
          <Text style={[styles.label, { color: colors.textSecondary }]}>Mot *</Text>
          <TextInput
            style={inputStyle}
            value={fields.Word}
            onChangeText={(text) => {
              set('Word')(text);
              setErrorMessage('');
            }}
            autoFocus={!isEditing}
          />

          {apiClient.isConfigured() && (
            <View style={styles.aiRow}>
              <Pressable
                style={[
                  styles.aiButton,
                  { borderColor: aiColor, backgroundColor: colors.surface },
                ]}
                onPress={handleEnrich}
                disabled={!canRunAi || isEnriching}
              >
                {isEnriching ? (
                  <ActivityIndicator size="small" color={aiColor} />
                ) : (
                  <CategoryIcon iconKey="Phosphor.sparkle" color={aiColor} size={14} />
                )}
                <Text style={{ color: aiColor, fontSize: 13, fontWeight: '600' }}>Enrichir</Text>
              </Pressable>
            </View>
          )}
          {enrichmentError.length > 0 && (
            <Text style={[styles.error, { color: colors.danger }]}>{enrichmentError}</Text>
          )}

          <View style={styles.typeRow}>
            <View style={styles.typeColumn}>
              <View style={styles.labelRow}>
                <Text style={[styles.label, styles.labelInRow, { color: colors.textSecondary }]}>Type</Text>
                <LockToggle locked={fields.LockedFields.includes('Type')} onToggle={toggleLock('Type')} />
              </View>
              <TypeDropdown selected={fields.Type} onChange={set('Type')} />
            </View>
            <View style={styles.archivedToggle}>
              <Text style={[styles.label, { color: colors.textSecondary }]}>Archivé</Text>
              <Switch value={fields.IsArchived} onValueChange={set('IsArchived')} />
            </View>
          </View>

          <View style={styles.labelRow}>
            <Text style={[styles.label, styles.labelInRow, { color: colors.textSecondary }]}>Définitions *</Text>
            <LockToggle
              locked={fields.LockedFields.includes('Definition')}
              onToggle={toggleLock('Definition')}
            />
          </View>
          <SenseListEditor senses={fields.Definition} onChange={set('Definition')} />

          <View style={styles.categorySection}>
            <CollapsibleSection title="Catégories">
              <CategoryChecklist
                selectedIds={fields.CategoryIds}
                onChange={set('CategoryIds')}
                onCenterSelected={centerOnRow}
              />
            </CollapsibleSection>
          </View>

          {apiClient.isConfigured() && (
            <View style={styles.aiRow}>
              <Pressable
                style={[
                  styles.aiButton,
                  { borderColor: aiColor, backgroundColor: colors.surface },
                ]}
                onPress={handleCategorize}
                disabled={!canRunAi || isCategorizing}
              >
                {isCategorizing ? (
                  <ActivityIndicator size="small" color={aiColor} />
                ) : (
                  <CategoryIcon iconKey="Phosphor.sparkle" color={aiColor} size={14} />
                )}
                <Text style={{ color: aiColor, fontSize: 13, fontWeight: '600' }}>Catégoriser</Text>
              </Pressable>
            </View>
          )}
          {categorizationError.length > 0 && (
            <Text style={[styles.error, { color: colors.danger }]}>{categorizationError}</Text>
          )}

          <View style={styles.labelRow}>
            <Text style={[styles.label, styles.labelInRow, { color: colors.textSecondary }]}>Synonymes</Text>
            <LockToggle
              locked={fields.LockedFields.includes('Synonyms')}
              onToggle={toggleLock('Synonyms')}
            />
          </View>
          <TextInput
            style={inputStyle}
            value={fields.SynonymsText}
            onChangeText={set('SynonymsText')}
            placeholderTextColor={colors.textMuted}
          />

          <View style={styles.labelRow}>
            <Text style={[styles.label, styles.labelInRow, { color: colors.textSecondary }]}>Exemples</Text>
            <LockToggle
              locked={fields.LockedFields.includes('ExampleSentences')}
              onToggle={toggleLock('ExampleSentences')}
            />
          </View>
          <TextInput
            style={[inputStyle, styles.multiline]}
            value={fields.ExampleSentencesText}
            onChangeText={set('ExampleSentencesText')}
            placeholderTextColor={colors.textMuted}
            multiline
          />

          <Text style={[styles.label, { color: colors.textSecondary }]}>Notes personnelles</Text>
          <TextInput
            style={[inputStyle, styles.multiline]}
            value={fields.Notes}
            onChangeText={set('Notes')}
            placeholderTextColor={colors.textMuted}
            multiline
          />

          <Text style={[styles.label, { color: colors.textSecondary }]}>Source</Text>
          <TextInput
            style={inputStyle}
            value={fields.Source}
            onChangeText={set('Source')}
            placeholderTextColor={colors.textMuted}
          />

          <Text style={[styles.label, { color: colors.textSecondary }]}>Images</Text>
          <EntryImagePicker images={fields.Images} onChange={set('Images')} />

          {errorMessage.length > 0 && (
            <Text style={[styles.error, { color: colors.danger }]}>{errorMessage}</Text>
          )}
        </ScrollView>

        <View
          style={[
            styles.footer,
            {
              backgroundColor: colors.surface,
              borderTopColor: colors.borderSubtle,
              paddingBottom: 14 + insets.bottom,
            },
          ]}
        >
          <Pressable
            style={[styles.saveButton, { backgroundColor: colors.accent }]}
            onPress={handleSave}
          >
            <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={16} />
            <Text style={[styles.saveText, { color: colors.textOnAccent }]}>
              {isEditing ? 'Enregistrer' : 'Ajouter'}
            </Text>
          </Pressable>
        </View>
      </KeyboardAvoidingView>

      {enrichmentSuggestions && (
        <EnrichmentReviewModal
          visible
          word={fields.Word.trim()}
          currentDefinition={fields.Definition}
          currentType={fields.Type}
          currentSynonyms={parseCommaSeparatedText(fields.SynonymsText)}
          currentExampleSentences={parseLineSeparatedText(fields.ExampleSentencesText)}
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
          currentCategoryNames={fields.CategoryIds
            .map((categoryId) => categoryIndex.get(categoryId)?.Name)
            .filter(Boolean)}
          onClose={() => setCategorizationSuggestions(null)}
          onSave={handleSaveCategorization}
        />
      )}
    </>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 24, gap: 6 },
  label: { fontSize: 12, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.4, marginTop: 16 },
  // A label paired with a LockToggle: the row carries the spacing instead of
  // the label text, so the icon lines up with it instead of sitting below.
  labelRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 16 },
  labelInRow: { marginTop: 0 },
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 10, fontSize: 15 },
  multiline: { minHeight: 80, textAlignVertical: 'top' },
  typeRow: { flexDirection: 'row', gap: 16, alignItems: 'flex-start' },
  typeColumn: { flex: 1, gap: 6 },
  archivedToggle: { alignItems: 'center', marginTop: 16 },
  categorySection: { marginTop: 16 },
  aiRow: { flexDirection: 'row', marginTop: 10 },
  aiButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 7,
    borderWidth: 1.5,
    borderRadius: 6,
    paddingHorizontal: 14,
    paddingVertical: 4,
  },
  error: { fontSize: 13, marginTop: 12 },
  footer: { borderTopWidth: 1, padding: 14 },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderRadius: 12,
    paddingVertical: 13,
  },
  saveText: { fontSize: 16, fontWeight: '700' },
});
