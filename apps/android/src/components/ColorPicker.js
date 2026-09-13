import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';
import { paletteSwatches } from '../utils/categoryColor';

const SWATCHES = paletteSwatches();

// Bottom sheet: the fixed swatch palette (same hues as the automatic
// golden-angle color, so a manual pick blends in) plus "Automatique" to
// clear the override.
export function ColorPicker({ visible, currentColorHex, onSelect, onClose }) {
  const colors = useTheme();

  function pick(hex) {
    onSelect(hex);
    onClose();
  }

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable style={[styles.sheet, { backgroundColor: colors.surface }]}>
          <Text style={[styles.title, { color: colors.textPrimary }]}>Choisir une couleur</Text>

          <View style={styles.grid}>
            {SWATCHES.map((hex) => {
              const selected = hex === currentColorHex;
              return (
                <Pressable
                  key={hex}
                  onPress={() => pick(hex)}
                  style={[
                    styles.swatch,
                    { backgroundColor: hex, borderColor: selected ? colors.textPrimary : 'transparent' },
                  ]}
                >
                  {selected && <CategoryIcon iconKey="Phosphor.check" color="#FFFFFF" size={16} />}
                </Pressable>
              );
            })}
          </View>

          <Pressable
            style={[styles.automatic, { borderColor: colors.borderStrong }]}
            onPress={() => pick(null)}
          >
            <Text style={[styles.automaticLabel, { color: colors.textPrimary }]}>Automatique</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0, 0, 0, 0.5)' },
  sheet: { borderTopLeftRadius: 18, borderTopRightRadius: 18, padding: 16, paddingBottom: 28 },
  title: { fontSize: 14, fontWeight: '600', textAlign: 'center', marginBottom: 14 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  swatch: {
    width: 40,
    height: 40,
    borderRadius: 20,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  automatic: {
    marginTop: 16,
    borderWidth: 1,
    borderRadius: 12,
    paddingVertical: 12,
    alignItems: 'center',
  },
  automaticLabel: { fontSize: 15, fontWeight: '600' },
});
