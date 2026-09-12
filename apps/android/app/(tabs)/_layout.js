import { Tabs, useRouter } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { SyncStatusIndicator } from '../../src/components/SyncStatusIndicator';
import { useTheme } from '../../src/theme/useTheme';

// Bottom bar holding every top-level destination — the app has no floating
// action button, so any global action belongs here, including "create".
export default function TabsLayout() {
  const colors = useTheme();
  const router = useRouter();

  const icon = (iconKey) =>
    function TabIcon({ color, size }) {
      return <CategoryIcon iconKey={iconKey} color={color} size={size} />;
    };

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.textPrimary,
        headerRight: () => <SyncStatusIndicator />,
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
        name="create"
        options={{
          title: '',
          tabBarLabel: () => null,
          tabBarIcon: ({ color }) => (
            <View style={[styles.createBadge, { backgroundColor: colors.accent }]}>
              <CategoryIcon iconKey="Phosphor.plus" color={colors.textOnAccent} size={22} />
            </View>
          ),
        }}
        listeners={{
          tabPress: (event) => {
            event.preventDefault();
            router.push('/entry/edit');
          },
        }}
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

const styles = StyleSheet.create({
  createBadge: {
    width: 44,
    height: 44,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
  },
});
