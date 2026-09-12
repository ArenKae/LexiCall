// Prepares a picked image before it's attached to an entry: downscale to a max
// dimension, then JPEG compression, so the local database stays light.
import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';

const MAX_DIMENSION_PIXELS = 1024;
const JPEG_COMPRESSION = 0.8;

export async function processImage(uri, width, height) {
  const largestSide = Math.max(width, height);
  const context = ImageManipulator.manipulate(uri);

  if (largestSide > MAX_DIMENSION_PIXELS) {
    // Constrain whichever side is largest; the manipulator derives the other
    // to preserve the aspect ratio.
    const resize = width >= height ? { width: MAX_DIMENSION_PIXELS } : { height: MAX_DIMENSION_PIXELS };
    context.resize(resize);
  }

  const rendered = await context.renderAsync();
  const saved = await rendered.saveAsync({
    compress: JPEG_COMPRESSION,
    format: SaveFormat.JPEG,
    base64: true,
  });

  return saved.base64;
}
