import { useMemo } from 'react';
import { useVocabularyStore } from '../store/useVocabularyStore';
import { computeColorIndexes } from '../utils/categoryHierarchy';
import { resolveCategoryColor } from '../utils/categoryColor';

// A category with no icon of its own still gets one.
const DEFAULT_ICON = 'Solar.tag';

// Categories by Id, each carrying its resolved color and icon, for the chips and
// rows that only hold a CategoryId.
export function useCategoryIndex() {
  const categories = useVocabularyStore((state) => state.categories);
  const categoryColors = useVocabularyStore((state) => state.categoryColors);

  return useMemo(() => {
    const colorIndexes = computeColorIndexes(categories);
    const categoriesById = new Map(categories.map((category) => [category.Id, category]));

    return new Map(
      categories.map((category) => [
        category.Id,
        {
          ...category,
          color: resolveCategoryColor(category, categoriesById, colorIndexes, categoryColors),
          icon: category.IconGlyph || DEFAULT_ICON,
        },
      ])
    );
  }, [categories, categoryColors]);
}
