// Free-text form fields to/from lists, e.g. "rapide, vif" becomes ["rapide", "vif"].

export function parseCommaSeparatedText(value) {
  // Also splits on ';' and '-' — a hyphenated synonym typed here gets split too.
  return value
    .split(/[,;-]/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

export function parseLineSeparatedText(value) {
  return value
    .split(/\r\n|\r|\n/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

export function formatCommaSeparatedText(values) {
  return values.join(', ');
}

export function formatLineSeparatedText(values) {
  return values.join('\n');
}
