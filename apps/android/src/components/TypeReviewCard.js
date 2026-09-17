import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { SuggestionCardShell } from './SuggestionCardShell';
import { useTheme } from '../theme/useTheme';
import { SELECTABLE_TYPES } from '../utils/vocabularyEntryTypes';

// Review card for the Type suggestion — unlike the other fields, this is a
// set of grammatical types, not free text. Pre-ticked with what was
// suggested, but every box stays tickable, same correction affordance as the
// text fields' editable text.
export function TypeReviewCard({
  accepted,
  onToggleAccepted,
  currentValueDisplay,
  justification,
  selected,
  onChangeSelected,
}) {
  const colors = useTheme();

  const toggle = (type) =>
    onChangeSelected(
      selected.includes(type) ? selected.filter((item) => item !== type) : [...selected, type]
    );

  return (
    <SuggestionCardShell
      label="Type"
      accepted={accepted}
      onToggleAccepted={onToggleAccepted}
      currentValueDisplay={currentValueDisplay}
      justification={justification}
    >
      <View style={styles.list}>
        {SELECTABLE_TYPES.map((type) => {
          const isSelected = selected.includes(type);
          return (
            <Pressable key={type} style={styles.row} onPress={() => toggle(type)}>
              <View
                style={[
                  styles.checkbox,
                  {
                    borderColor: isSelected ? colors.accent : colors.borderStrong,
                    backgroundColor: isSelected ? colors.accent : 'transparent',
                  },
                ]}
              >
                {isSelected && <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={12} />}
              </View>
              <Text style={[styles.rowLabel, { color: colors.textPrimary }]}>{type}</Text>
            </Pressable>
          );
        })}
      </View>
    </SuggestionCardShell>
  );
}

const styles = StyleSheet.create({
  list: { gap: 2 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 6 },
  checkbox: {
    width: 18,
    height: 18,
    borderRadius: 5,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  rowLabel: { fontSize: 14 },
});
