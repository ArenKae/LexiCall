import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategoryIcon } from './CategoryIcon';
import { CategoryParentPicker } from './CategoryParentPicker';
import { IconPicker } from './IconPicker';
import { SuggestionCardShell } from './SuggestionCardShell';
import { useTheme } from '../theme/useTheme';
import { useVocabularyStore } from '../store/useVocabularyStore';
import { flattenCategories } from '../utils/categoryHierarchy';

const DEFAULT_ICON = 'Solar.tag';

// Closed, shows the picked category's name; open, a flat indented list —
// unlike CategoryParentPicker there's no "no category" option here, since
// there's always Reject on the card itself for that.
function ExistingCategoryPicker({ selectedId, onChange }) {
  const colors = useTheme();
  const insets = useSafeAreaInsets();
  const categories = useVocabularyStore((state) => state.categories);
  const categoryOrder = useVocabularyStore((state) => state.categoryOrder);
  const [open, setOpen] = useState(false);
  const options = flattenCategories(categories, categoryOrder);
  const selected = categories.find((category) => category.Id === selectedId);

  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        style={[styles.field, { borderColor: colors.borderSubtle, backgroundColor: colors.background }]}
      >
        <Text style={[styles.fieldText, { color: colors.textPrimary }]} numberOfLines={1}>
          {selected?.Name ?? 'Choisir une catégorie…'}
        </Text>
        <CategoryIcon iconKey="Phosphor.caret-down" color={colors.textMuted} size={14} />
      </Pressable>

      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)}>
          <Pressable
            style={[
              styles.sheet,
              { backgroundColor: colors.surface, paddingBottom: 28 + insets.bottom },
            ]}
          >
            <ScrollView style={styles.list}>
              {options.map(({ category, depth }) => (
                <Pressable
                  key={category.Id}
                  style={[styles.row, { marginLeft: depth * 16 }]}
                  onPress={() => {
                    onChange(category.Id);
                    setOpen(false);
                  }}
                >
                  <Text style={[styles.rowLabel, { color: colors.textPrimary }]} numberOfLines={1}>
                    {category.Name}
                  </Text>
                  {selectedId === category.Id && (
                    <CategoryIcon iconKey="Phosphor.check" color={colors.accent} size={16} />
                  )}
                </Pressable>
              ))}
            </ScrollView>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

// One auto-categorization suggestion: attach to an existing category, or
// create a new one — the LLM's decision is just a starting point, fully
// overridable. Controlled from the parent modal, which owns everything
// needed to build the result at save time.
export function CategorySuggestionCard({ suggestion, currentCategoryNames, card, onChange }) {
  const colors = useTheme();
  const [iconPickerOpen, setIconPickerOpen] = useState(false);

  return (
    <SuggestionCardShell
      label={card.isNewCategory ? 'Nouvelle catégorie' : 'Catégorie existante'}
      accepted={card.accepted}
      onToggleAccepted={(accepted) => onChange({ accepted })}
      currentValueDisplay={currentCategoryNames.length === 0 ? null : currentCategoryNames.join(', ')}
      justification={suggestion.justification}
    >
      <View style={styles.modeRow}>
        <Pressable
          style={[
            styles.modeButton,
            {
              borderColor: !card.isNewCategory ? colors.accent : colors.borderStrong,
              backgroundColor: !card.isNewCategory ? colors.selectionBackground : 'transparent',
            },
          ]}
          onPress={() => onChange({ isNewCategory: false })}
        >
          <Text style={{ color: colors.textPrimary, fontSize: 13, fontWeight: '600' }}>Existante</Text>
        </Pressable>
        <Pressable
          style={[
            styles.modeButton,
            {
              borderColor: card.isNewCategory ? colors.accent : colors.borderStrong,
              backgroundColor: card.isNewCategory ? colors.selectionBackground : 'transparent',
            },
          ]}
          onPress={() => onChange({ isNewCategory: true })}
        >
          <Text style={{ color: colors.textPrimary, fontSize: 13, fontWeight: '600' }}>Nouvelle</Text>
        </Pressable>
      </View>

      {card.isNewCategory ? (
        <View style={styles.newCategoryFields}>
          <View style={styles.nameRow}>
            <TextInput
              style={[
                styles.field,
                styles.nameInput,
                { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.background },
              ]}
              value={card.newCategoryName}
              onChangeText={(text) => onChange({ newCategoryName: text })}
              placeholder="Nom de la catégorie"
              placeholderTextColor={colors.textMuted}
            />
            <Pressable
              style={[styles.iconButton, { borderColor: colors.borderSubtle, backgroundColor: colors.background }]}
              onPress={() => setIconPickerOpen(true)}
            >
              <CategoryIcon
                iconKey={card.newCategoryIconGlyph || DEFAULT_ICON}
                color={colors.textPrimary}
                size={18}
              />
            </Pressable>
          </View>

          <CategoryParentPicker
            selectedId={card.newCategoryParentId}
            onChange={(id) => onChange({ newCategoryParentId: id })}
          />

          <TextInput
            style={[
              styles.field,
              styles.descriptionInput,
              { color: colors.textPrimary, borderColor: colors.borderSubtle, backgroundColor: colors.background },
            ]}
            value={card.newCategoryDescription}
            onChangeText={(text) => onChange({ newCategoryDescription: text })}
            placeholder="Description (optionnel)"
            placeholderTextColor={colors.textMuted}
            multiline
          />

          <IconPicker
            visible={iconPickerOpen}
            currentIconKey={card.newCategoryIconGlyph}
            onSelect={(iconKey) => onChange({ newCategoryIconGlyph: iconKey })}
            onClose={() => setIconPickerOpen(false)}
          />
        </View>
      ) : (
        <ExistingCategoryPicker
          selectedId={card.existingCategoryId}
          onChange={(id) => onChange({ existingCategoryId: id })}
        />
      )}
    </SuggestionCardShell>
  );
}

const styles = StyleSheet.create({
  modeRow: { flexDirection: 'row', gap: 8 },
  modeButton: {
    flex: 1,
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 8,
    paddingVertical: 8,
  },
  newCategoryFields: { gap: 8 },
  nameRow: { flexDirection: 'row', gap: 8 },
  nameInput: { flex: 1 },
  iconButton: {
    width: 40,
    height: 40,
    borderRadius: 10,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  field: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
  },
  fieldText: { flex: 1, fontSize: 14 },
  descriptionInput: { minHeight: 60, textAlignVertical: 'top' },
  backdrop: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0, 0, 0, 0.5)' },
  sheet: { borderTopLeftRadius: 18, borderTopRightRadius: 18, padding: 16, paddingBottom: 28 },
  list: { maxHeight: 420 },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 11 },
  rowLabel: { fontSize: 15, flex: 1 },
});
