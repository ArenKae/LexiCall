import { Pressable } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

// Excludes one field from the next AI enrichment call — never that the field
// is read-only in the form itself.
export function LockToggle({ locked, onToggle }) {
  const colors = useTheme();

  return (
    <Pressable onPress={() => onToggle(!locked)} hitSlop={10}>
      <CategoryIcon
        iconKey={locked ? 'Phosphor.lock-key' : 'Phosphor.lock-key-open'}
        color={locked ? colors.danger : colors.textSecondary}
        size={15}
      />
    </Pressable>
  );
}
