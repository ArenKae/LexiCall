// Which entries a given sidebar selection shows.
import { compareText } from './collation';
import { getDescendantIds } from './categoryHierarchy';
import { entryMatchesSearch, entryWordMatchesSearch, normalizeForSearch } from './search';

export const ALL_ENTRIES = 'all';
export const UNCATEGORIZED = 'uncategorized';
export const ARCHIVES = 'archives';

export const SORT_RECENT = 'recent';
export const SORT_ALPHABETICAL = 'alphabetical';

function compareBySortMode(a, b, sortMode) {
  if (sortMode === SORT_RECENT) {
    // Newest first; falls back to Word so two entries created in the same
    // instant (or with an unparsable CreatedAt) still land in a stable order.
    const delta = Date.parse(b.CreatedAt) - Date.parse(a.CreatedAt);
    return Number.isNaN(delta) || delta === 0 ? compareText(a.Word, b.Word) : delta;
  }
  return compareText(a.Word, b.Word);
}

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

export function selectEntries({ entries, categories, filter, query, sortMode = SORT_RECENT }) {
  const subtreeIds =
    filter.kind === 'category' && filter.categoryId
      ? new Set([filter.categoryId, ...getDescendantIds(categories, filter.categoryId)])
      : new Set();

  const normalizedQuery = normalizeForSearch(query ?? '');
  const categoryNamesById = new Map(categories.map((category) => [category.Id, category.Name]));

  const matching = entries.filter(
    (entry) =>
      entryMatchesFilter(entry, filter, subtreeIds) &&
      entryMatchesSearch(entry, normalizedQuery, categoryNamesById)
  );

  // An entry matched only in its definition/synonyms/etc. is a weaker hit than
  // one matched in the word itself — rank the latter first. The selected sort
  // mode breaks ties (and is the sole key outside a search).
  return matching.sort((a, b) => {
    const rankDelta =
      Number(entryWordMatchesSearch(b, normalizedQuery)) -
      Number(entryWordMatchesSearch(a, normalizedQuery));
    return rankDelta !== 0 ? rankDelta : compareBySortMode(a, b, sortMode);
  });
}
