import { Pressable, StyleSheet, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

export function CloseDisc({ size = 32 }) {
  const colors = useTheme();

  return (
    <View style={[styles.disc, { width: size, height: size, backgroundColor: colors.dangerTint }]}>
      <CategoryIcon iconKey="Phosphor.x-bold" color={colors.danger} size={size / 2} />
    </View>
  );
}

export function CloseButton({ onPress, size = 32, style }) {
  return (
    <Pressable
      onPress={onPress}
      hitSlop={8}
      style={({ pressed }) => [style, { opacity: pressed ? 0.6 : 1 }]}
    >
      <CloseDisc size={size} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  disc: { borderRadius: 999, alignItems: 'center', justifyContent: 'center' },
});
