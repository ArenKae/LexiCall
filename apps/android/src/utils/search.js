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

const normalizedEntries = new WeakMap();
const normalizedNames = new Map();

function normalizedEntry(entry) {
  let normalized = normalizedEntries.get(entry);
  if (!normalized) {
    const fields = [
      entry.Word,
      ...entry.Definition,
      entry.Notes,
      entry.Source,
      ...entry.Synonyms,
      ...entry.ExampleSentences,
    ];
    normalized = {
      word: normalizeForSearch(entry.Word),
      text: fields.map(normalizeForSearch).join('\u0000'),
    };
    normalizedEntries.set(entry, normalized);
  }
  return normalized;
}

function normalizedName(name) {
  let normalized = normalizedNames.get(name);
  if (normalized === undefined) {
    normalized = normalizeForSearch(name);
    normalizedNames.set(name, normalized);
  }
  return normalized;
}

// Whether the pattern shows up in the word itself, as opposed to only in some
// other field — used to rank a direct match above an incidental one.
export function entryWordMatchesSearch(entry, normalizedQuery) {
  return normalizedEntry(entry).word.includes(normalizedQuery);
}

export function entryMatchesSearch(entry, normalizedQuery, categoryNamesById) {
  // A blank query filters nothing — typing a single space must not empty the
  // list.
  if (normalizedQuery.trim().length === 0) {
    return true;
  }

  return (
    normalizedEntry(entry).text.includes(normalizedQuery) ||
    entry.CategoryIds.some((id) => {
      const name = categoryNamesById.get(id);
      return Boolean(name) && normalizedName(name).includes(normalizedQuery);
    })
  );
}
