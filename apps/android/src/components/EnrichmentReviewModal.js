import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { DefinitionReviewCard } from './DefinitionReviewCard';
import { TextFieldReviewCard } from './TextFieldReviewCard';
import { TypeReviewCard } from './TypeReviewCard';
import { useTheme } from '../theme/useTheme';
import {
  formatCommaSeparatedText,
  formatLineSeparatedText,
  parseCommaSeparatedText,
  parseLineSeparatedText,
} from '../utils/textListParser';
import { SELECTABLE_TYPES, UNDEFINED_TYPE } from '../utils/vocabularyEntryTypes';

function sensesFromValues(values) {
  const senses = (values ?? []).map((text) => ({ text, rephraseLocked: false, rephraseAnchor: null }));
  return senses.length > 0 ? senses : [{ text: '', rephraseLocked: false, rephraseAnchor: null }];
}

// Full-screen review for a combined enrichment suggestion (POST
// /enrichment/fields response) — one card per field the API actually
// suggested something for, each independently acceptable/correctable/
// rejectable. Persists nothing itself: onSave only ever receives the
// accepted values, the caller decides what to do with them.
export function EnrichmentReviewModal({
  visible,
  word,
  currentDefinition,
  currentType,
  currentSynonyms,
  currentExampleSentences,
  suggestions,
  apiClient,
  onClose,
  onSave,
}) {
  const colors = useTheme();

  const [definitionAccepted, setDefinitionAccepted] = useState(true);
  const [senses, setSenses] = useState(() => sensesFromValues(suggestions.definition?.value));
  const [typeAccepted, setTypeAccepted] = useState(true);
  const [types, setTypes] = useState(() => suggestions.type?.value ?? []);
  const [synonymsAccepted, setSynonymsAccepted] = useState(true);
  const [synonymsText, setSynonymsText] = useState(() =>
    formatCommaSeparatedText(suggestions.synonyms?.value ?? [])
  );
  const [examplesAccepted, setExamplesAccepted] = useState(true);
  const [examplesText, setExamplesText] = useState(() =>
    formatLineSeparatedText(suggestions.example_sentences?.value ?? [])
  );

  const currentTypeLabels = currentType.filter((type) => SELECTABLE_TYPES.includes(type));

  function handleSave() {
    const result = {};

    if (suggestions.definition && definitionAccepted) {
      const trimmed = senses.map((sense) => sense.text.trim()).filter((text) => text.length > 0);
      if (trimmed.length > 0) {
        result.Definition = trimmed;
      }
    }
    if (suggestions.type && typeAccepted) {
      result.Type = types.length > 0 ? types : [UNDEFINED_TYPE];
    }
    if (suggestions.synonyms && synonymsAccepted) {
      result.Synonyms = parseCommaSeparatedText(synonymsText);
    }
    if (suggestions.example_sentences && examplesAccepted) {
      result.ExampleSentences = parseLineSeparatedText(examplesText);
    }

    onSave(result);
  }

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <View style={styles.header}>
          <Text style={[styles.title, { color: colors.textPrimary }]}>Suggestions IA</Text>
          <Pressable onPress={onClose} hitSlop={10}>
            <CategoryIcon iconKey="Phosphor.x" color={colors.textSecondary} size={20} />
          </Pressable>
        </View>

        <ScrollView contentContainerStyle={styles.content}>
          {suggestions.definition && (
            <DefinitionReviewCard
              accepted={definitionAccepted}
              onToggleAccepted={setDefinitionAccepted}
              currentValueDisplay={
                currentDefinition.length === 0 ? null : formatLineSeparatedText(currentDefinition)
              }
              justification={suggestions.definition.justification}
              word={word}
              apiClient={apiClient}
              senses={senses}
              onChangeSenses={setSenses}
            />
          )}

          {suggestions.type && (
            <TypeReviewCard
              accepted={typeAccepted}
              onToggleAccepted={setTypeAccepted}
              currentValueDisplay={currentTypeLabels.length === 0 ? null : currentTypeLabels.join(', ')}
              justification={suggestions.type.justification}
              selected={types}
              onChangeSelected={setTypes}
            />
          )}

          {suggestions.synonyms && (
            <TextFieldReviewCard
              label="Synonymes"
              accepted={synonymsAccepted}
              onToggleAccepted={setSynonymsAccepted}
              currentValueDisplay={
                currentSynonyms.length === 0 ? null : formatCommaSeparatedText(currentSynonyms)
              }
              justification={suggestions.synonyms.justification}
              value={synonymsText}
              onChangeValue={setSynonymsText}
            />
          )}

          {suggestions.example_sentences && (
            <TextFieldReviewCard
              label="Exemples"
              accepted={examplesAccepted}
              onToggleAccepted={setExamplesAccepted}
              currentValueDisplay={
                currentExampleSentences.length === 0
                  ? null
                  : formatLineSeparatedText(currentExampleSentences)
              }
              justification={suggestions.example_sentences.justification}
              value={examplesText}
              onChangeValue={setExamplesText}
              multiline
            />
          )}
        </ScrollView>

        <View style={[styles.footer, { backgroundColor: colors.surface, borderTopColor: colors.borderSubtle }]}>
          <Pressable style={[styles.saveButton, { backgroundColor: colors.accent }]} onPress={handleSave}>
            <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={16} />
            <Text style={[styles.saveText, { color: colors.textOnAccent }]}>Enregistrer</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, paddingTop: 54 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingBottom: 14,
  },
  title: { fontSize: 18, fontWeight: '700' },
  content: { paddingHorizontal: 16, paddingBottom: 24, gap: 12 },
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
