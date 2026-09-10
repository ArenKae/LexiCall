import { useColorScheme } from 'react-native';
import { lightColors, darkColors } from './colors';

// Returns the color palette matching the current OS light/dark setting.
export function useTheme() {
  const scheme = useColorScheme();
  return scheme === 'dark' ? darkColors : lightColors;
}
