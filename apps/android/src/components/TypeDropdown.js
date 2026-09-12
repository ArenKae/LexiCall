import { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';
import { SELECTABLE_TYPES } from '../utils/vocabularyEntryTypes';

const UNDEFINED_LABEL = 'Non défini';

// Closed, it shows the selection joined into one line (or "Non défini" when
// empty); open, it lists every type as a checkbox. Nothing ticked is the
// untyped state, stored as [Undefined], never as [].
export function TypeDropdown({ selected, onChange }) {
  const colors = useTheme();
  const [open, setOpen] = useState(false);

  const toggle = (type) => {
    onChange(
      selected.includes(type) ? selected.filter((item) => item !== type) : [...selected, type]
    );
  };

  // selected can legitimately be the literal ['Undefined'] — never tickable,
  // so it must be filtered out here too before checking for an empty result.
  const realTypes = selected.filter((type) => SELECTABLE_TYPES.includes(type));
  const summary = realTypes.length > 0 ? realTypes.join(', ') : UNDEFINED_LABEL;

  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        style={[styles.field, { borderColor: colors.borderSubtle, backgroundColor: colors.surface }]}
      >
        <Text style={[styles.summary, { color: colors.textPrimary }]} numberOfLines={1}>
          {summary}
        </Text>
        <CategoryIcon iconKey="Phosphor.caret-down" color={colors.textMuted} size={14} />
      </Pressable>

      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)}>
          <Pressable style={[styles.sheet, { backgroundColor: colors.surface }]}>
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
                    {isSelected && (
                      <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={12} />
                    )}
                  </View>
                  <Text style={[styles.rowLabel, { color: colors.textPrimary }]}>{type}</Text>
                </Pressable>
              );
            })}

            <Pressable
              style={[styles.done, { backgroundColor: colors.accent }]}
              onPress={() => setOpen(false)}
            >
              <Text style={{ color: colors.textOnAccent, fontWeight: '700' }}>OK</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  field: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  summary: { flex: 1, fontSize: 15 },
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0, 0, 0, 0.5)' },
  sheet: { borderTopLeftRadius: 18, borderTopRightRadius: 18, padding: 16, paddingBottom: 28, gap: 2 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 11 },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 5,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  rowLabel: { fontSize: 15 },
  done: { marginTop: 12, borderRadius: 12, paddingVertical: 12, alignItems: 'center' },
});
