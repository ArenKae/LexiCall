// API client for the LexiCall backend: X-API-Key auth, connectivity check
// via /health then /auth, and delta pulls of entries and categories.

const CONNECTION_TIMEOUT_MS = 2000;
// Pulls are given far more room than the connectivity check: the very first one
// carries the whole collection over a phone's network, not a two-byte reply.
const PULL_TIMEOUT_MS = 20000;
// An entry push can carry up to four base64 images in the same request.
const PUSH_TIMEOUT_MS = 30000;

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

  // Writes resolve to a plain boolean instead of throwing like pull() does:
  // a resync pushes record by record and must keep going past a single
  // failure, marking only what actually landed.
  async function upsert(path, record) {
    if (!isConfigured()) {
      return false;
    }

    try {
      const response = await fetchWithTimeout(
        `${root}${path}/${record.Id}`,
        {
          method: 'PUT',
          headers: { 'X-API-Key': apiKey, 'Content-Type': 'application/json' },
          // Sent whole, extra sync-only fields included: the API's write
          // models don't declare them, so they're dropped server-side.
          body: JSON.stringify(record),
        },
        PUSH_TIMEOUT_MS
      );
      return response.ok;
    } catch {
      return false;
    }
  }

  async function remove(path, id, deletedAt) {
    if (!isConfigured()) {
      return false;
    }

    try {
      const response = await fetchWithTimeout(
        `${root}${path}/${id}?deleted_at=${encodeURIComponent(deletedAt)}`,
        { method: 'DELETE', headers: { 'X-API-Key': apiKey } },
        PUSH_TIMEOUT_MS
      );
      // 404 means it was never synced — nothing to delete, not a failure.
      return response.ok || response.status === 404;
    } catch {
      return false;
    }
  }

  return {
    isConfigured,
    testConnection,
    imageRequest,
    pullEntries: (updatedSince) => pull('/entries', updatedSince),
    pullCategories: (updatedSince) => pull('/categories', updatedSince),
    upsertEntry: (entry) => upsert('/entries', entry),
    upsertCategory: (category) => upsert('/categories', category),
    deleteEntry: (id, deletedAt) => remove('/entries', id, deletedAt),
    deleteCategory: (id, deletedAt) => remove('/categories', id, deletedAt),
  };
}
