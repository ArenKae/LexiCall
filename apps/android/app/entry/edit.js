import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useState } from 'react';
import {
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
import { CategoryChecklist } from '../../src/components/CategoryChecklist';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { CollapsibleSection } from '../../src/components/CollapsibleSection';
import { EntryImagePicker } from '../../src/components/EntryImagePicker';
import { SenseListEditor } from '../../src/components/SenseListEditor';
import { TypeDropdown } from '../../src/components/TypeDropdown';
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
  };
}

// Create/edit form for one entry. Only Word and at least one non-empty sense
// are required — everything else is optional, same as the field itself allows.
export default function EntryEditor() {
  const { id } = useLocalSearchParams();
  const colors = useTheme();
  const router = useRouter();
  const existingEntry = useVocabularyStore((state) => state.entries.find((entry) => entry.Id === id));
  const categoryFilter = useVocabularyStore((state) => state.categoryFilter);
  const addEntry = useVocabularyStore((state) => state.addEntry);
  const updateEntry = useVocabularyStore((state) => state.updateEntry);

  const isEditing = existingEntry !== undefined;
  const [fields, setFields] = useState(() =>
    isEditing
      ? fieldFromEntry(existingEntry)
      : blankFields(categoryFilter.kind === 'category' ? categoryFilter.categoryId : null)
  );
  const [errorMessage, setErrorMessage] = useState('');

  const set = (key) => (value) => setFields((current) => ({ ...current, [key]: value }));

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
      LockedFields: existingEntry?.LockedFields ?? [],
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
        <ScrollView contentContainerStyle={styles.content}>
          <Text style={[styles.label, { color: colors.textSecondary }]}>Mot *</Text>
          <TextInput
            style={inputStyle}
            value={fields.Word}
            onChangeText={(text) => {
              set('Word')(text);
              setErrorMessage('');
            }}
            placeholder="Le mot ou l’expression"
            placeholderTextColor={colors.textMuted}
            autoFocus={!isEditing}
          />

          <View style={styles.typeRow}>
            <View style={styles.typeColumn}>
              <Text style={[styles.label, { color: colors.textSecondary }]}>Type</Text>
              <TypeDropdown selected={fields.Type} onChange={set('Type')} />
            </View>
            <View style={styles.archivedToggle}>
              <Text style={[styles.label, { color: colors.textSecondary }]}>Archivé</Text>
              <Switch value={fields.IsArchived} onValueChange={set('IsArchived')} />
            </View>
          </View>

          <Text style={[styles.label, { color: colors.textSecondary }]}>Définitions *</Text>
          <SenseListEditor senses={fields.Definition} onChange={set('Definition')} />

          <View style={styles.categorySection}>
            <CollapsibleSection title="Catégories">
              <CategoryChecklist selectedIds={fields.CategoryIds} onChange={set('CategoryIds')} />
            </CollapsibleSection>
          </View>

          <Text style={[styles.label, { color: colors.textSecondary }]}>Synonymes</Text>
          <TextInput
            style={inputStyle}
            value={fields.SynonymsText}
            onChangeText={set('SynonymsText')}
            placeholderTextColor={colors.textMuted}
          />

          <Text style={[styles.label, { color: colors.textSecondary }]}>Exemples</Text>
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

        <View style={[styles.footer, { backgroundColor: colors.surface, borderTopColor: colors.borderSubtle }]}>
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
    </>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 24, gap: 6 },
  label: { fontSize: 12, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.4, marginTop: 16 },
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 10, fontSize: 15 },
  multiline: { minHeight: 80, textAlignVertical: 'top' },
  typeRow: { flexDirection: 'row', gap: 16, alignItems: 'flex-start' },
  typeColumn: { flex: 1 },
  archivedToggle: { alignItems: 'center', marginTop: 16 },
  categorySection: { marginTop: 16 },
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
