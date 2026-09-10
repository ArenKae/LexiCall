import { Tabs } from 'expo-router';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { useTheme } from '../../src/theme/useTheme';

// Bottom bar holding every top-level destination — the app has no floating
// action button, so any global action belongs here.
export default function TabsLayout() {
  const colors = useTheme();

  const icon = (iconKey) =>
    function TabIcon({ color, size }) {
      return <CategoryIcon iconKey={iconKey} color={color} size={size} />;
    };

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.textPrimary,
        sceneStyle: { backgroundColor: colors.background },
        tabBarStyle: { backgroundColor: colors.surface, borderTopColor: colors.borderSubtle },
        tabBarActiveTintColor: colors.accent,
        tabBarInactiveTintColor: colors.textMuted,
      }}
    >
      <Tabs.Screen
        name="index"
        options={{ title: 'Accueil', tabBarIcon: icon('Phosphor.house') }}
      />
      <Tabs.Screen
        name="categories"
        options={{ title: 'Catégories', tabBarIcon: icon('Phosphor.stack') }}
      />
      <Tabs.Screen
        name="search"
        options={{ title: 'Recherche', tabBarIcon: icon('Phosphor.magnifying-glass') }}
      />
      <Tabs.Screen
        name="more"
        options={{ title: 'Plus', tabBarIcon: icon('Phosphor.dots-three-circle-vertical') }}
      />
    </Tabs>
  );
}
