import { StyleSheet, TextInput } from 'react-native';
import { SuggestionCardShell } from './SuggestionCardShell';
import { useTheme } from '../theme/useTheme';

// Review card for a text-shaped enrichment suggestion (Synonymes, Exemples) —
// both arrive already formatted as editable text by the caller.
export function TextFieldReviewCard({
  label,
  accepted,
  onToggleAccepted,
  currentValueDisplay,
  justification,
  value,
  onChangeValue,
  multiline,
}) {
  const colors = useTheme();

  return (
    <SuggestionCardShell
      label={label}
      accepted={accepted}
      onToggleAccepted={onToggleAccepted}
      currentValueDisplay={currentValueDisplay}
      justification={justification}
    >
      <TextInput
        style={[
          styles.input,
          multiline && styles.multiline,
          { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.background },
        ]}
        value={value}
        onChangeText={onChangeValue}
        multiline={multiline}
      />
    </SuggestionCardShell>
  );
}

const styles = StyleSheet.create({
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 9, fontSize: 14 },
  multiline: { minHeight: 70, textAlignVertical: 'top' },
});
