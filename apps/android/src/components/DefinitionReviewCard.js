import { useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { LockToggle } from './LockToggle';
import { SuggestionCardShell } from './SuggestionCardShell';
import { useTheme } from '../theme/useTheme';

// Définition review card — one row per sense, plus the "Reformuler" action
// unique to this field: POST /enrichment/rephrase-definition rephrases one
// sense at a time, so a locked sense is left out and keeps its wording.
// senses: [{ text, rephraseLocked, rephraseAnchor }].
export function DefinitionReviewCard({
  accepted,
  onToggleAccepted,
  currentValueDisplay,
  justification,
  word,
  apiClient,
  senses,
  onChangeSenses,
}) {
  const colors = useTheme();
  const [isRephrasing, setIsRephrasing] = useState(false);
  const [rephraseError, setRephraseError] = useState('');

  const update = (index, patch) =>
    onChangeSenses(senses.map((sense, i) => (i === index ? { ...sense, ...patch } : sense)));

  const remove = (index) => {
    const next = senses.filter((_, i) => i !== index);
    onChangeSenses(next.length > 0 ? next : [{ text: '', rephraseLocked: false, rephraseAnchor: null }]);
  };

  const add = () =>
    onChangeSenses([...senses, { text: '', rephraseLocked: false, rephraseAnchor: null }]);

  const rephraseTargets = senses
    .map((sense, index) => ({ sense, index }))
    .filter(({ sense }) => !sense.rephraseLocked && sense.text.trim().length > 0);
  const canRephrase = !isRephrasing && rephraseTargets.length > 0;

  async function handleRephrase() {
    if (!canRephrase) {
      return;
    }

    setRephraseError('');
    setIsRephrasing(true);

    // The anchor is captured once (first rephrase) and reused on every later
    // call for that sense — reusing the latest result instead would drift
    // further from the original meaning with each successive rephrase.
    const anchors = rephraseTargets.map(({ sense }) => sense.rephraseAnchor ?? sense.text);
    const results = await Promise.all(
      anchors.map((anchor) => apiClient.rephraseDefinition(word, anchor))
    );

    setIsRephrasing(false);

    const next = [...senses];
    let failure = null;

    rephraseTargets.forEach(({ index }, i) => {
      const { status, result, errorDetail } = results[i];
      next[index] = { ...next[index], rephraseAnchor: anchors[i] };

      if (status === 'Ok' && result) {
        next[index].text = result.definition;
      } else if (!failure) {
        failure = { status, errorDetail };
      }
    });

    onChangeSenses(next);

    if (failure) {
      setRephraseError(
        failure.status === 'NotConfigured'
          ? 'La reformulation nécessite une synchronisation API configurée (voir Options).'
          : failure.errorDetail
            ? `Impossible de reformuler : ${failure.errorDetail}`
            : 'Impossible de reformuler pour le moment. Réessaie plus tard.'
      );
    }
  }

  return (
    <SuggestionCardShell
      label="Définition"
      accepted={accepted}
      onToggleAccepted={onToggleAccepted}
      currentValueDisplay={currentValueDisplay}
      justification={justification}
    >
      <View style={styles.senses}>
        {senses.map((sense, index) => (
          // eslint-disable-next-line react/no-array-index-key -- rows have no stable id, only position
          <View key={index} style={styles.row}>
            {senses.length > 1 && (
              <Text style={[styles.number, { color: colors.textSecondary }]}>{index + 1}.</Text>
            )}
            <TextInput
              style={[
                styles.input,
                { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.background },
              ]}
              value={sense.text}
              onChangeText={(text) => update(index, { text })}
              multiline
            />
            <LockToggle
              locked={sense.rephraseLocked}
              onToggle={(locked) => update(index, { rephraseLocked: locked })}
            />
            {senses.length > 1 && (
              <Pressable onPress={() => remove(index)} hitSlop={8} style={styles.remove}>
                <CategoryIcon iconKey="Phosphor.x" color={colors.textMuted} size={15} />
              </Pressable>
            )}
          </View>
        ))}

        <Pressable onPress={add} style={styles.addButton}>
          <CategoryIcon iconKey="Phosphor.plus" color={colors.accent} size={14} />
          <Text style={[styles.addText, { color: colors.accent }]}>Ajouter un sens</Text>
        </Pressable>
      </View>

      <Pressable
        style={[styles.rephrase, { borderColor: colors.borderStrong, opacity: canRephrase ? 1 : 0.5 }]}
        onPress={handleRephrase}
        disabled={!canRephrase}
      >
        {isRephrasing ? (
          <ActivityIndicator size="small" color={colors.textSecondary} />
        ) : (
          <CategoryIcon iconKey="Phosphor.arrows-counter-clockwise" color={colors.textSecondary} size={14} />
        )}
        <Text style={{ color: colors.textSecondary, fontSize: 13, fontWeight: '600' }}>
          {isRephrasing ? 'Reformulation…' : 'Reformuler'}
        </Text>
      </Pressable>
      {rephraseError.length > 0 && (
        <Text style={[styles.rephraseError, { color: colors.danger }]}>{rephraseError}</Text>
      )}
    </SuggestionCardShell>
  );
}

const styles = StyleSheet.create({
  senses: { gap: 8 },
  row: { flexDirection: 'row', alignItems: 'flex-start', gap: 8 },
  number: { fontSize: 14, marginTop: 10 },
  input: {
    flex: 1,
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 9,
    fontSize: 14,
    minHeight: 38,
  },
  remove: { marginTop: 10 },
  addButton: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingVertical: 4 },
  addText: { fontSize: 13, fontWeight: '600' },
  rephrase: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    alignSelf: 'flex-start',
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 7,
  },
  rephraseError: { fontSize: 12 },
});
