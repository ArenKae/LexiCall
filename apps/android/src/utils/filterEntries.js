// Which entries a given sidebar selection shows, matching the desktop client's
// rules exactly.
import { compareText } from './collation';
import { getDescendantIds } from './categoryHierarchy';
import { entryMatchesSearch, normalizeForSearch } from './search';

export const ALL_ENTRIES = 'all';
export const UNCATEGORIZED = 'uncategorized';
export const ARCHIVES = 'archives';

export function entryMatchesFilter(entry, filter, subtreeIds) {
  if (filter.kind === ARCHIVES) {
    return entry.IsArchived;
  }
  if (filter.kind === UNCATEGORIZED) {
    return !entry.IsArchived && entry.CategoryIds.length === 0;
  }
  if (filter.kind === ALL_ENTRIES) {
    return !entry.IsArchived;
  }
  // Under a real category an archived entry stays visible: it is only hidden
  // from the two catch-all views above.
  return entry.CategoryIds.some((id) => subtreeIds.has(id));
}

export function selectEntries({ entries, categories, filter, query }) {
  const subtreeIds =
    filter.kind === 'category' && filter.categoryId
      ? new Set([filter.categoryId, ...getDescendantIds(categories, filter.categoryId)])
      : new Set();

  const normalizedQuery = normalizeForSearch(query ?? '');
  const categoryNamesById = new Map(categories.map((category) => [category.Id, category.Name]));

  return entries
    .filter(
      (entry) =>
        entryMatchesFilter(entry, filter, subtreeIds) &&
        entryMatchesSearch(entry, normalizedQuery, categoryNamesById)
    )
    .sort((a, b) => compareText(a.Word, b.Word));
}
