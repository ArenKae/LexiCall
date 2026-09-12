import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../theme/useTheme';
import { SORT_ALPHABETICAL, SORT_RECENT } from '../utils/filterEntries';

const OPTIONS = [
  { value: SORT_RECENT, label: 'Plus récent' },
  { value: SORT_ALPHABETICAL, label: 'Alphabétique' },
];

// Segmented sort-mode picker. The selection is in-memory only, never written
// to settings.json — it resets to the default on every app launch.
export function SortControl({ value, onChange }) {
  const colors = useTheme();

  return (
    <View style={styles.row}>
      {OPTIONS.map((option) => {
        const isActive = option.value === value;
        return (
          <Pressable
            key={option.value}
            onPress={() => onChange(option.value)}
            style={[
              styles.segment,
              { backgroundColor: isActive ? colors.accent : colors.chipBackground },
            ]}
          >
            <Text style={{ color: isActive ? colors.textOnAccent : colors.chipForeground, fontSize: 12, fontWeight: '600' }}>
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', gap: 6 },
  segment: { paddingHorizontal: 10, paddingVertical: 5, borderRadius: 999 },
});
