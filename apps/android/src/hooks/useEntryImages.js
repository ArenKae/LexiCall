import { useCallback, useEffect, useState } from 'react';
import { createApiClient } from '../services/apiClient';
import { cachedImageUri, inlineImageUri, loadImage } from '../services/imageCache';
import { useVocabularyStore } from '../store/useVocabularyStore';

// Per-image load state for an entry, keyed by image Id: { status, uri }.
// ImageBase64 is non-empty only for an image added on this device and never
// yet synced — that case is used directly, never fetched. A pull carries no
// bytes at all, so anything else not already cached is fetched here.
export function useEntryImages(entry) {
  const apiBaseUrl = useVocabularyStore((state) => state.apiBaseUrl);
  const apiKey = useVocabularyStore((state) => state.apiKey);
  const [states, setStates] = useState({});

  const fetchOne = useCallback(
    (entryId, imageId) => {
      setStates((current) => ({ ...current, [imageId]: { status: 'loading' } }));

      return loadImage(createApiClient(apiBaseUrl, apiKey), entryId, imageId)
        .then((uri) => setStates((current) => ({ ...current, [imageId]: { status: 'ready', uri } })))
        .catch((error) => {
          // The native message (connection refused, HTTP status…) is useful
          // when debugging but far too technical to put on screen.
          console.warn(`[image ${imageId}] ${String(error?.message ?? error)}`);
          setStates((current) => ({ ...current, [imageId]: { status: 'failed' } }));
        });
    },
    [apiBaseUrl, apiKey]
  );

  useEffect(() => {
    if (!entry) {
      return;
    }

    const initial = {};
    const missing = [];

    for (const image of entry.Images) {
      const cached = cachedImageUri(image.Id);

      if (image.ImageBase64.length > 0) {
        initial[image.Id] = { status: 'ready', uri: inlineImageUri(image.ImageBase64) };
      } else if (cached) {
        initial[image.Id] = { status: 'ready', uri: cached };
      } else {
        initial[image.Id] = { status: 'loading' };
        missing.push(image.Id);
      }
    }

    setStates(initial);
    for (const imageId of missing) {
      fetchOne(entry.Id, imageId);
    }
  }, [entry, fetchOne]);

  const retry = useCallback(
    (imageId) => entry && fetchOne(entry.Id, imageId),
    [entry, fetchOne]
  );

  return { states, retry };
}
