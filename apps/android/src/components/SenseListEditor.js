import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

// One row per sense of the definition: add and remove freely, no reordering.
export function SenseListEditor({ senses, onChange }) {
  const colors = useTheme();

  const update = (index, text) => onChange(senses.map((sense, i) => (i === index ? text : sense)));

  const remove = (index) => {
    const next = senses.filter((_, i) => i !== index);
    onChange(next.length > 0 ? next : ['']);
  };

  const add = () => onChange([...senses, '']);

  return (
    <View style={styles.container}>
      {senses.map((sense, index) => (
        // eslint-disable-next-line react/no-array-index-key -- rows have no stable id, only position
        <View key={index} style={styles.row}>
          {senses.length > 1 && (
            <Text style={[styles.number, { color: colors.textSecondary }]}>{index + 1}.</Text>
          )}
          <TextInput
            style={[
              styles.input,
              { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.surface },
            ]}
            value={sense}
            onChangeText={(text) => update(index, text)}
            placeholder="Sens du mot…"
            placeholderTextColor={colors.textMuted}
            multiline
          />
          {senses.length > 1 && (
            <Pressable onPress={() => remove(index)} hitSlop={8} style={styles.remove}>
              <CategoryIcon iconKey="Phosphor.x" color={colors.textMuted} size={16} />
            </Pressable>
          )}
        </View>
      ))}

      <Pressable onPress={add} style={styles.addButton}>
        <CategoryIcon iconKey="Phosphor.plus" color={colors.accent} size={15} />
        <Text style={[styles.addText, { color: colors.accent }]}>Ajouter un sens</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 8 },
  row: { flexDirection: 'row', alignItems: 'flex-start', gap: 8 },
  number: { fontSize: 15, marginTop: 10 },
  input: {
    flex: 1,
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 9,
    fontSize: 14,
    minHeight: 40,
  },
  remove: { marginTop: 10 },
  addButton: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingVertical: 6 },
  addText: { fontSize: 13, fontWeight: '600' },
});
