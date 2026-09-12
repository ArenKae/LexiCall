// Chronological comparison of the ISO-8601 timestamps that drive Last-Write-Wins.

// A single API response mixes formats ("...645328Z" next to "...+02:00" with no
// fractional part at all), so lexicographic order says nothing useful here —
// both sides must be parsed before being compared.
export function isNewer(candidate, reference) {
  const candidateTime = Date.parse(candidate);
  const referenceTime = Date.parse(reference);

  if (Number.isNaN(candidateTime)) {
    return false;
  }
  if (Number.isNaN(referenceTime)) {
    return true;
  }
  return candidateTime > referenceTime;
}
