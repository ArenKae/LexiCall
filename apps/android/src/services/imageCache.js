// On-disk cache for entry images. A pull only carries image metadata, so the
// bytes are fetched once per image and kept afterwards. An image Id is never
// reused for different bytes — editing an image creates a new one — so a
// cached file never needs invalidating.
import { Directory, File, Paths } from 'expo-file-system';

const CACHE_DIRECTORY = 'entry-images';

// Dedupes concurrent requests for the same image, which two thumbnails of the
// same entry would otherwise fire at once.
const inFlight = new Map();

function cachedFile(imageId) {
  const directory = new Directory(Paths.cache, CACHE_DIRECTORY);

  if (!directory.exists) {
    directory.create({ intermediates: true });
  }
  return new File(directory, `${imageId}.jpg`);
}

export function cachedImageUri(imageId) {
  const file = cachedFile(imageId);
  return file.exists ? file.uri : null;
}

// A data URI from bytes already held in memory — no disk cache, no network.
export function inlineImageUri(base64) {
  return `data:image/jpeg;base64,${base64}`;
}

export async function loadImage(client, entryId, imageId) {
  const cached = cachedImageUri(imageId);

  if (cached) {
    return cached;
  }
  if (inFlight.has(imageId)) {
    return inFlight.get(imageId);
  }

  const { url, headers } = client.imageRequest(entryId, imageId);
  const download = File.downloadFileAsync(url, cachedFile(imageId), { headers, idempotent: true })
    .then((file) => file.uri)
    .finally(() => inFlight.delete(imageId));

  inFlight.set(imageId, download);
  return download;
}
