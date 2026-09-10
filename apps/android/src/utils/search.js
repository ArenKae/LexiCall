// Accent-insensitive, in-memory entry search — no index, a plain scan over a
// few hundred entries.

// Decomposes then drops combining marks, so "ephemere" matches "Éphémère".
// Hyphens also collapse to spaces ("vide gousset" finds "Vide-gousset"), and the
// two apostrophes in use ("L'ire" straight, "À l’envi" typographic) are folded
// together — NFD leaves those distinct, they are not accent variants.
export function normalizeForSearch(value) {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/\p{Mn}/gu, '')
    .normalize('NFC')
    .replace(/[-‐‑–—]/g, ' ')
    .replace(/[’ʼ]/g, "'")
    .toLowerCase();
}

function fieldMatches(value, normalizedQuery) {
  return normalizeForSearch(value).includes(normalizedQuery);
}

function anyFieldMatches(values, normalizedQuery) {
  return values.some((value) => fieldMatches(value, normalizedQuery));
}

export function entryMatchesSearch(entry, normalizedQuery, categoryNamesById) {
  // A blank query filters nothing — typing a single space must not empty the
  // list.
  if (normalizedQuery.trim().length === 0) {
    return true;
  }

  return (
    fieldMatches(entry.Word, normalizedQuery) ||
    anyFieldMatches(entry.Definition, normalizedQuery) ||
    fieldMatches(entry.Notes, normalizedQuery) ||
    fieldMatches(entry.Source, normalizedQuery) ||
    anyFieldMatches(entry.Synonyms, normalizedQuery) ||
    anyFieldMatches(entry.ExampleSentences, normalizedQuery) ||
    anyFieldMatches(
      entry.CategoryIds.map((id) => categoryNamesById.get(id)).filter(Boolean),
      normalizedQuery
    )
  );
}
