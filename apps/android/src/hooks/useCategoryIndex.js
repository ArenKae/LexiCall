import { useMemo } from 'react';
import { useVocabularyStore } from '../store/useVocabularyStore';
import { computeColorIndexes } from '../utils/categoryHierarchy';
import { colorFromIndex } from '../utils/categoryColor';

// A category with no icon of its own still gets one.
const DEFAULT_ICON = 'Solar.tag';

// Categories by Id, each carrying its resolved color and icon, for the chips and
// rows that only hold a CategoryId.
export function useCategoryIndex() {
  const categories = useVocabularyStore((state) => state.categories);

  return useMemo(() => {
    const colorIndexes = computeColorIndexes(categories);

    return new Map(
      categories.map((category) => [
        category.Id,
        {
          ...category,
          color: colorFromIndex(colorIndexes.get(category.Id) ?? 0),
          icon: category.IconGlyph || DEFAULT_ICON,
        },
      ])
    );
  }, [categories]);
}
