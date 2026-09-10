// API client for the LexiCall backend: X-API-Key auth, connectivity check
// via /health then /auth.

const TIMEOUT_MS = 2000;

async function fetchWithTimeout(url, options) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

export function createApiClient(baseUrl, apiKey) {
  async function testConnection() {
    if (!baseUrl || !apiKey) {
      return 'NotConfigured';
    }
    try {
      const health = await fetchWithTimeout(`${baseUrl}/health`);
      if (!health.ok) {
        return 'Unreachable';
      }
      const auth = await fetchWithTimeout(`${baseUrl}/auth`, {
        headers: { 'X-API-Key': apiKey },
      });
      if (auth.status === 401 || auth.status === 403) {
        return 'InvalidApiKey';
      }
      return auth.ok ? 'Ok' : 'Unreachable';
    } catch {
      return 'Unreachable';
    }
  }

  return { testConnection };
}
