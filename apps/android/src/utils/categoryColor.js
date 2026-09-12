// A category's automatic color: a hue spaced by the golden angle, derived from
// its root category so a whole subtree shares one color.

const GOLDEN_ANGLE = 137.508;
const SATURATION = 0.5;
const LIGHTNESS = 0.52;

// Replicates .NET's Guid.GetHashCode (the four 32-bit words of the GUID's
// in-memory layout, XORed) so a category keeps the exact same automatic color
// on both clients. The layout is little-endian for the first three fields and
// raw byte order for the last eight.
function guidHashCode(id) {
  const hex = String(id).replace(/-/g, '');

  if (hex.length !== 32) {
    return 0;
  }

  const byteAt = (index) => parseInt(hex.slice(index * 2, index * 2 + 2), 16);
  const littleEndianWord = (offset) =>
    (byteAt(offset) |
      (byteAt(offset + 1) << 8) |
      (byteAt(offset + 2) << 16) |
      (byteAt(offset + 3) << 24)) |
    0;

  // The textual form writes the first three fields big-endian, while the words
  // being XORed are read straight out of memory: the int32, then the two
  // shorts sitting side by side as one little-endian word.
  const first = ((byteAt(0) << 24) | (byteAt(1) << 16) | (byteAt(2) << 8) | byteAt(3)) | 0;
  const shorts = ((byteAt(6) << 24) | (byteAt(7) << 16) | (byteAt(4) << 8) | byteAt(5)) | 0;

  return (first ^ shorts ^ littleEndianWord(8) ^ littleEndianWord(12)) | 0;
}

export function colorIndexForRoot(categoryId) {
  return guidHashCode(categoryId) & 0x7fffffff;
}

function hslToHex(hue, saturation, lightness) {
  const chroma = (1 - Math.abs(2 * lightness - 1)) * saturation;
  const huePrime = hue / 60;
  const secondary = chroma * (1 - Math.abs((huePrime % 2) - 1));

  let rgb;
  if (huePrime < 1) rgb = [chroma, secondary, 0];
  else if (huePrime < 2) rgb = [secondary, chroma, 0];
  else if (huePrime < 3) rgb = [0, chroma, secondary];
  else if (huePrime < 4) rgb = [0, secondary, chroma];
  else if (huePrime < 5) rgb = [secondary, 0, chroma];
  else rgb = [chroma, 0, secondary];

  const match = lightness - chroma / 2;
  const channel = (value) =>
    Math.round((value + match) * 255)
      .toString(16)
      .padStart(2, '0');

  return `#${channel(rgb[0])}${channel(rgb[1])}${channel(rgb[2])}`;
}

export function colorFromIndex(index) {
  return hslToHex(((index * GOLDEN_ANGLE) % 360), SATURATION, LIGHTNESS);
}
