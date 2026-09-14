import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { CategoryIcon } from './CategoryIcon';
import { CategorySuggestionCard } from './CategorySuggestionCard';
import { useTheme } from '../theme/useTheme';

function cardFromSuggestion(suggestion) {
  return {
    accepted: true,
    isNewCategory: suggestion.decision === 'new',
    existingCategoryId: suggestion.category?.id ?? null,
    newCategoryName: suggestion.new_category_name ?? '',
    newCategoryParentId: suggestion.new_category_parent?.id ?? null,
    newCategoryDescription: '',
    newCategoryIconGlyph: '',
  };
}

// One result entry, or null on an incomplete selection (mode "new" with an
// empty name, or mode "existing" with nothing picked) — silently dropped
// rather than blocking the whole save, same "when in doubt, apply nothing"
// bias as the rest of the review.
function buildResult(card) {
  if (card.isNewCategory) {
    const name = card.newCategoryName.trim();
    if (name.length === 0) {
      return null;
    }
    return {
      existingCategoryId: null,
      newCategoryName: name,
      newCategoryParentId: card.newCategoryParentId,
      newCategoryDescription: card.newCategoryDescription.trim(),
      newCategoryIconGlyph: card.newCategoryIconGlyph,
    };
  }

  return card.existingCategoryId
    ? {
        existingCategoryId: card.existingCategoryId,
        newCategoryName: null,
        newCategoryParentId: null,
        newCategoryDescription: null,
        newCategoryIconGlyph: null,
      }
    : null;
}

// Full-screen review for auto-categorization suggestions (POST
// /enrichment/categorize response) — usually one card, several only when the
// word carries genuinely distinct senses across different lexical fields.
// Persists nothing itself: onSave only receives the accepted, complete
// results.
export function CategorizationReviewModal({ visible, suggestions, currentCategoryNames, onClose, onSave }) {
  const colors = useTheme();
  const insets = useSafeAreaInsets();
  const [cards, setCards] = useState(() => suggestions.map(cardFromSuggestion));

  const updateCard = (index, patch) =>
    setCards((current) => current.map((card, i) => (i === index ? { ...card, ...patch } : card)));

  function handleSave() {
    const results = cards
      .filter((card) => card.accepted)
      .map(buildResult)
      .filter((result) => result !== null);

    onSave(results);
  }

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <View style={styles.header}>
          <Text style={[styles.title, { color: colors.textPrimary }]}>Suggestions de catégorie</Text>
          <Pressable onPress={onClose} hitSlop={10}>
            <CategoryIcon iconKey="Phosphor.x" color={colors.textSecondary} size={20} />
          </Pressable>
        </View>

        <ScrollView contentContainerStyle={styles.content}>
          {suggestions.map((suggestion, index) => (
            // eslint-disable-next-line react/no-array-index-key -- suggestions have no stable id, only position
            <CategorySuggestionCard
              key={index}
              suggestion={suggestion}
              currentCategoryNames={currentCategoryNames}
              card={cards[index]}
              onChange={(patch) => updateCard(index, patch)}
            />
          ))}
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
            <Text style={[styles.saveText, { color: colors.textOnAccent }]}>Enregistrer</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, paddingTop: 54 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingBottom: 14,
  },
  title: { fontSize: 18, fontWeight: '700' },
  content: { paddingHorizontal: 16, paddingBottom: 24, gap: 12 },
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
