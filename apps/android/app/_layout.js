import { useEffect } from 'react';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { useColorScheme } from 'react-native';
import { useTheme } from '../src/theme/useTheme';
import { useVocabularyStore } from '../src/store/useVocabularyStore';

// Root navigation: the tab bar plus the screens that cover it, and the one
// place the local database is read back into the store on launch.
export default function RootLayout() {
  const scheme = useColorScheme();
  const colors = useTheme();
  const hydrate = useVocabularyStore((state) => state.hydrate);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  return (
    <SafeAreaProvider>
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.surface },
          headerTintColor: colors.textPrimary,
          contentStyle: { backgroundColor: colors.background },
        }}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="entry/[id]" options={{ title: '' }} />
        <Stack.Screen name="options" options={{ title: 'Options' }} />
      </Stack>
      <StatusBar style={scheme === 'dark' ? 'light' : 'dark'} />
    </SafeAreaProvider>
  );
}
