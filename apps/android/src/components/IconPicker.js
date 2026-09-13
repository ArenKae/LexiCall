import { useMemo, useState } from 'react';
import { FlatList, Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';
import { ICON_GROUPS } from '../theme/iconCatalog';

// Full-screen picker over the generated icon catalog's groups, filtered by a
// plain case-insensitive substring match against each icon's keywords. The
// keywords are already unaccented, but the typed query isn't folded to match
// — a deliberate limitation, not a bug: "café" won't find a "cafe" keyword.
export function IconPicker({ visible, currentIconKey, onSelect, onClose }) {
  const colors = useTheme();
  const [query, setQuery] = useState('');

  const groups = useMemo(() => {
    const needle = query.trim().toLowerCase();

    if (!needle) {
      return ICON_GROUPS;
    }

    return ICON_GROUPS.map((group) => ({
      ...group,
      icons: group.icons.filter((icon) => icon.keywords.toLowerCase().includes(needle)),
    })).filter((group) => group.icons.length > 0);
  }, [query]);

  function pick(iconKey) {
    onSelect(iconKey);
    onClose();
  }

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <View style={styles.header}>
          <View
            style={[
              styles.searchField,
              { backgroundColor: colors.surface, borderColor: colors.borderSubtle },
            ]}
          >
            <CategoryIcon iconKey="Phosphor.magnifying-glass" color={colors.textMuted} size={16} />
            <TextInput
              style={[styles.searchInput, { color: colors.textPrimary }]}
              value={query}
              onChangeText={setQuery}
              placeholder="Rechercher une icône…"
              placeholderTextColor={colors.textMuted}
              autoCapitalize="none"
              autoCorrect={false}
            />
            {query.length > 0 && (
              <Pressable onPress={() => setQuery('')} hitSlop={8}>
                <CategoryIcon iconKey="Phosphor.x" color={colors.textMuted} size={15} />
              </Pressable>
            )}
          </View>
          <Pressable onPress={onClose} hitSlop={10} style={styles.closeButton}>
            <Text style={[styles.closeLabel, { color: colors.accent }]}>Fermer</Text>
          </Pressable>
        </View>

        <FlatList
          data={groups}
          keyExtractor={(group) => group.name}
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          ListEmptyComponent={
            <Text style={[styles.empty, { color: colors.textSecondary }]}>Aucune icône trouvée.</Text>
          }
          renderItem={({ item: group }) => (
            <View style={styles.group}>
              <Text style={[styles.groupTitle, { color: colors.textSecondary }]}>{group.name}</Text>
              <View style={styles.grid}>
                {group.icons.map((icon) => {
                  const selected = icon.iconKey === currentIconKey;
                  return (
                    <Pressable
                      key={icon.iconKey}
                      onPress={() => pick(icon.iconKey)}
                      style={[
                        styles.iconButton,
                        {
                          backgroundColor: selected ? colors.selectionBackground : colors.surface,
                          borderColor: selected ? colors.accent : colors.borderSubtle,
                        },
                      ]}
                    >
                      <CategoryIcon iconKey={icon.iconKey} color={colors.textPrimary} size={22} />
                    </Pressable>
                  );
                })}
              </View>
            </View>
          )}
        />
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, paddingTop: 54 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 14, paddingBottom: 12 },
  searchField: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    height: 42,
  },
  searchInput: { flex: 1, fontSize: 15, paddingVertical: 0 },
  closeButton: { paddingHorizontal: 4, paddingVertical: 6 },
  closeLabel: { fontSize: 15, fontWeight: '600' },
  list: { paddingHorizontal: 14, paddingBottom: 24 },
  group: { marginTop: 16 },
  groupTitle: { fontSize: 13, fontWeight: '700', marginBottom: 8 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  iconButton: {
    width: 44,
    height: 44,
    borderRadius: 10,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  empty: { padding: 28, textAlign: 'center' },
});
