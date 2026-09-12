// Locale-aware, accent-insensitive comparison for every sorted list.

// String.localeCompare builds a collator on each call — about 40x the cost of
// reusing one, which on a few hundred rows is the difference between typing
// feeling instant and feeling laggy.
const collator = new Intl.Collator('fr', { sensitivity: 'base' });

export function compareText(a, b) {
  return collator.compare(a ?? '', b ?? '');
}
