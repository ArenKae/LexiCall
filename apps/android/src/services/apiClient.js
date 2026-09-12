// API client for the LexiCall backend: X-API-Key auth, connectivity check
// via /health then /auth, and delta pulls of entries and categories.

const CONNECTION_TIMEOUT_MS = 2000;
// Pulls are given far more room than the connectivity check: the very first one
// carries the whole collection over a phone's network, not a two-byte reply.
const PULL_TIMEOUT_MS = 20000;

async function fetchWithTimeout(url, options, timeoutMs) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

export function createApiClient(baseUrl, apiKey) {
  const root = baseUrl.replace(/\/+$/, '');

  const isConfigured = () => root.length > 0 && apiKey.length > 0;

  async function testConnection() {
    if (!isConfigured()) {
      return 'NotConfigured';
    }
    try {
      const health = await fetchWithTimeout(`${root}/health`, {}, CONNECTION_TIMEOUT_MS);
      if (!health.ok) {
        return 'Unreachable';
      }
      const auth = await fetchWithTimeout(
        `${root}/auth`,
        { headers: { 'X-API-Key': apiKey } },
        CONNECTION_TIMEOUT_MS
      );
      if (auth.status === 401 || auth.status === 403) {
        return 'InvalidApiKey';
      }
      return auth.ok ? 'Ok' : 'Unreachable';
    } catch {
      return 'Unreachable';
    }
  }

  // Resolves to { records, syncTimestamp }; throws on any failure so the caller
  // can leave its checkpoint untouched and retry the whole cycle later.
  async function pull(path, updatedSince) {
    if (!isConfigured()) {
      throw new Error('Synchronisation non configurée.');
    }

    const query = updatedSince ? `?updated_since=${encodeURIComponent(updatedSince)}` : '';
    const response = await fetchWithTimeout(
      `${root}${path}${query}`,
      { headers: { 'X-API-Key': apiKey } },
      PULL_TIMEOUT_MS
    );

    if (!response.ok) {
      throw new Error(`${path} : HTTP ${response.status}`);
    }

    return {
      records: await response.json(),
      // Server-issued token, kept verbatim as the next pull's checkpoint.
      syncTimestamp: response.headers.get('X-Sync-Timestamp'),
    };
  }

  // Raw bytes, not JSON: the caller streams this straight to a file.
  function imageRequest(entryId, imageId) {
    return {
      url: `${root}/entries/${entryId}/images/${imageId}`,
      headers: { 'X-API-Key': apiKey },
    };
  }

  return {
    isConfigured,
    testConnection,
    imageRequest,
    pullEntries: (updatedSince) => pull('/entries', updatedSince),
    pullCategories: (updatedSince) => pull('/categories', updatedSince),
  };
}
