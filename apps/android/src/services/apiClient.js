// API client for the LexiCall backend: X-API-Key auth, connectivity check
// via /health then /auth, and delta pulls of entries and categories.

const CONNECTION_TIMEOUT_MS = 2000;
// Pulls are given far more room than the connectivity check: the very first one
// carries the whole collection over a phone's network, not a two-byte reply.
const PULL_TIMEOUT_MS = 20000;
// An entry push can carry up to four base64 images in the same request.
const PUSH_TIMEOUT_MS = 30000;
// LLM-backed calls (2-6s typical, more with the web_search fallback) get far
// more room than the 2s tuned for silent background sync.
const ENRICHMENT_TIMEOUT_MS = 20000;

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

  // FastAPI's default error shape is {"detail": "..."} (HTTPException) or
  // {"detail": [...]} (422 validation errors) — fall back to the raw body,
  // then to the bare status code, for anything else (e.g. a proxy error page).
  async function readErrorDetail(response) {
    let body;
    try {
      body = await response.text();
    } catch {
      return `HTTP ${response.status}`;
    }
    if (!body) {
      return `HTTP ${response.status}`;
    }
    try {
      const parsed = JSON.parse(body);
      if (typeof parsed?.detail === 'string') {
        return parsed.detail;
      }
    } catch {
      // Not JSON — fall through to the raw body.
    }
    return body.length > 300 ? body.slice(0, 300) : body;
  }

  // Resolves to { status: 'NotConfigured' | 'Failed' | 'Ok', result, errorDetail }
  // — an explicit user action, not background sync, so the caller can show a
  // real error instead of a swallowed failure.
  async function postEnrichment(path, payload) {
    if (!isConfigured()) {
      return { status: 'NotConfigured', result: null, errorDetail: null };
    }

    try {
      const response = await fetchWithTimeout(
        `${root}${path}`,
        {
          method: 'POST',
          headers: { 'X-API-Key': apiKey, 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        },
        ENRICHMENT_TIMEOUT_MS
      );

      if (!response.ok) {
        return { status: 'Failed', result: null, errorDetail: await readErrorDetail(response) };
      }

      return { status: 'Ok', result: await response.json(), errorDetail: null };
    } catch (error) {
      return { status: 'Failed', result: null, errorDetail: String(error?.message ?? error) };
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
    // draft: { Word, Definition, Type, Synonyms, ExampleSentences, LockedFields }.
    suggestFields: (draft) => postEnrichment('/enrichment/fields', draft),
    // A single sense's wording — the caller always re-sends the original
    // anchor, never a previous rephrase's output, to avoid cumulative drift.
    rephraseDefinition: (word, definition) =>
      postEnrichment('/enrichment/rephrase-definition', { Word: word, Definition: definition }),
    categorize: (word, definition) =>
      postEnrichment('/enrichment/categorize', { Word: word, Definition: definition }),
    // Full repair pass over the category embeddings, recomputing whatever the
    // best-effort refresh following a category write missed. Nothing to send:
    // the server works out on its own what drifted.
    reindexCategoryEmbeddings: () => postEnrichment('/categories/reindex-embeddings', {}),
  };
}
