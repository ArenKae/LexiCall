import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

// Shared shell for one review card (enrichment field or category suggestion):
// a check-to-accept header, an optional before/after comparison, then
// whatever the field-specific content is. Pre-checked: unchecking is how the
// user rejects just this one card, independently of the others.
export function SuggestionCardShell({ label, accepted, onToggleAccepted, currentValueDisplay, justification, children }) {
  const colors = useTheme();

  return (
    <View
      style={[
        styles.card,
        { borderColor: accepted ? colors.accent : colors.borderSubtle, backgroundColor: colors.surface },
      ]}
    >
      <Pressable style={styles.header} onPress={() => onToggleAccepted(!accepted)}>
        <View
          style={[
            styles.checkbox,
            {
              borderColor: accepted ? colors.accent : colors.borderStrong,
              backgroundColor: accepted ? colors.accent : 'transparent',
            },
          ]}
        >
          {accepted && <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={13} />}
        </View>
        <Text style={[styles.label, { color: colors.textPrimary }]}>{label}</Text>
      </Pressable>

      {currentValueDisplay && (
        <View style={styles.currentValue}>
          <Text style={[styles.currentValueLabel, { color: colors.textMuted }]}>Actuel</Text>
          <Text style={[styles.currentValueText, { color: colors.textSecondary }]}>{currentValueDisplay}</Text>
        </View>
      )}

      {children}

      {justification && (
        <Text style={[styles.justification, { color: colors.textMuted }]}>{justification}</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  card: { borderWidth: 1.5, borderRadius: 14, padding: 14, gap: 10 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 5,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  label: { fontSize: 15, fontWeight: '700' },
  currentValue: { gap: 2 },
  currentValueLabel: { fontSize: 11, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.4 },
  currentValueText: { fontSize: 13, lineHeight: 18 },
  justification: { fontSize: 12, lineHeight: 16, fontStyle: 'italic' },
});
