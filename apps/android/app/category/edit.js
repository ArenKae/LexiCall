import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { CategoryParentPicker } from '../../src/components/CategoryParentPicker';
import { ColorPicker } from '../../src/components/ColorPicker';
import { IconPicker } from '../../src/components/IconPicker';
import { useTheme } from '../../src/theme/useTheme';
import { createCategoryDraft, useVocabularyStore } from '../../src/store/useVocabularyStore';

const DEFAULT_ICON = 'Solar.tag';

function fieldsFromCategory(category) {
  return {
    Name: category.Name,
    ParentId: category.ParentId,
    Description: category.Description,
    IconGlyph: category.IconGlyph,
  };
}

// Create/edit form for one category. Only the name is required.
export default function CategoryEditor() {
  const { id, parentId } = useLocalSearchParams();
  const colors = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const existingCategory = useVocabularyStore((state) =>
    state.categories.find((category) => category.Id === id)
  );
  const categoryColors = useVocabularyStore((state) => state.categoryColors);
  const addCategory = useVocabularyStore((state) => state.addCategory);
  const updateCategory = useVocabularyStore((state) => state.updateCategory);
  const setCategoryColor = useVocabularyStore((state) => state.setCategoryColor);
  const setEditorOpen = useVocabularyStore((state) => state.setEditorOpen);

  // Holds off the periodic resync: a pull landing mid-edit would swap the
  // record out from under the form.
  useEffect(() => {
    setEditorOpen(true);
    return () => setEditorOpen(false);
  }, [setEditorOpen]);

  const isEditing = existingCategory !== undefined;
  const [fields, setFields] = useState(() =>
    isEditing ? fieldsFromCategory(existingCategory) : createCategoryDraft(parentId ?? null)
  );
  const [colorHex, setColorHex] = useState(() =>
    isEditing ? (categoryColors[existingCategory.Id] ?? null) : null
  );
  const [iconPickerOpen, setIconPickerOpen] = useState(false);
  const [colorPickerOpen, setColorPickerOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const set = (key) => (value) => setFields((current) => ({ ...current, [key]: value }));

  function handleSave() {
    const name = fields.Name.trim();

    if (name.length === 0) {
      setErrorMessage('Le nom est obligatoire.');
      return;
    }

    const payload = {
      Name: name,
      ParentId: fields.ParentId,
      Description: fields.Description.trim(),
      IconGlyph: fields.IconGlyph,
    };

    if (isEditing) {
      const error = updateCategory(existingCategory.Id, payload);

      if (error) {
        setErrorMessage(error);
        return;
      }

      setCategoryColor(existingCategory.Id, colorHex);
    } else {
      const { error, category } = addCategory(payload);

      if (error) {
        setErrorMessage(error);
        return;
      }

      setCategoryColor(category.Id, colorHex);
    }

    router.back();
  }

  const inputStyle = [
    styles.input,
    { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.surface },
  ];

  return (
    <>
      <Stack.Screen options={{ title: isEditing ? 'Modifier la catégorie' : 'Nouvelle catégorie' }} />
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={[styles.label, { color: colors.textSecondary }]}>Nom *</Text>
        <View style={styles.nameRow}>
          <TextInput
            style={[inputStyle, styles.nameInput]}
            value={fields.Name}
            onChangeText={(text) => {
              set('Name')(text);
              setErrorMessage('');
            }}
            placeholder="Nom de la catégorie"
            placeholderTextColor={colors.textMuted}
            autoFocus={!isEditing}
          />
          <Pressable
            style={[styles.iconButton, { borderColor: colors.borderSubtle, backgroundColor: colors.surface }]}
            onPress={() => setIconPickerOpen(true)}
          >
            <CategoryIcon iconKey={fields.IconGlyph || DEFAULT_ICON} color={colors.textPrimary} size={20} />
          </Pressable>
          {fields.IconGlyph.length > 0 && (
            <Pressable style={styles.clearIcon} onPress={() => set('IconGlyph')('')} hitSlop={8}>
              <CategoryIcon iconKey="Phosphor.x" color={colors.textMuted} size={13} />
            </Pressable>
          )}
        </View>

        <Text style={[styles.label, { color: colors.textSecondary }]}>Couleur</Text>
        <Pressable style={styles.colorRow} onPress={() => setColorPickerOpen(true)}>
          <View
            style={[
              styles.colorSwatch,
              { backgroundColor: colorHex ?? colors.background, borderColor: colors.borderStrong },
            ]}
          />
          <Text style={{ color: colors.textSecondary, fontSize: 14 }}>
            {colorHex ?? 'Automatique'}
          </Text>
          {colorHex && (
            <Pressable
              style={styles.clearColor}
              onPress={() => setColorHex(null)}
              hitSlop={8}
            >
              <Text style={{ color: colors.accent, fontSize: 13, fontWeight: '600' }}>Automatique</Text>
            </Pressable>
          )}
        </Pressable>

        <Text style={[styles.label, { color: colors.textSecondary }]}>Catégorie parente</Text>
        <CategoryParentPicker
          selectedId={fields.ParentId}
          excludeCategoryId={existingCategory?.Id}
          onChange={set('ParentId')}
        />

        <Text style={[styles.label, { color: colors.textSecondary }]}>Description</Text>
        <TextInput
          style={[inputStyle, styles.multiline]}
          value={fields.Description}
          onChangeText={set('Description')}
          placeholderTextColor={colors.textMuted}
          multiline
        />

        {errorMessage.length > 0 && (
          <Text style={[styles.error, { color: colors.danger }]}>{errorMessage}</Text>
        )}
      </ScrollView>

      <View
        style={[
          styles.footer,
          {
            backgroundColor: colors.surface,
            borderTopColor: colors.borderSubtle,
            paddingBottom: 14 + insets.bottom,
          },
        ]}
      >
        <Pressable style={[styles.saveButton, { backgroundColor: colors.accent }]} onPress={handleSave}>
          <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={16} />
          <Text style={[styles.saveText, { color: colors.textOnAccent }]}>
            {isEditing ? 'Enregistrer' : 'Créer'}
          </Text>
        </Pressable>
      </View>

      <IconPicker
        visible={iconPickerOpen}
        currentIconKey={fields.IconGlyph}
        onSelect={set('IconGlyph')}
        onClose={() => setIconPickerOpen(false)}
      />
      <ColorPicker
        visible={colorPickerOpen}
        currentColorHex={colorHex}
        onSelect={setColorHex}
        onClose={() => setColorPickerOpen(false)}
      />
    </>
  );
}

const styles = StyleSheet.create({
  content: { padding: 16, paddingBottom: 24, gap: 6 },
  label: { fontSize: 12, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.4, marginTop: 16 },
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 10, fontSize: 15 },
  multiline: { minHeight: 90, textAlignVertical: 'top' },
  nameRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  nameInput: { flex: 1 },
  iconButton: {
    width: 42,
    height: 42,
    borderRadius: 10,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  clearIcon: { padding: 4 },
  colorRow: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  colorSwatch: { width: 32, height: 32, borderRadius: 16, borderWidth: 2 },
  clearColor: { marginLeft: 'auto', paddingVertical: 6, paddingHorizontal: 4 },
  error: { fontSize: 13, marginTop: 12 },
  footer: { borderTopWidth: 1, padding: 14 },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderRadius: 12,
    paddingVertical: 13,
  },
  saveText: { fontSize: 16, fontWeight: '700' },
});
