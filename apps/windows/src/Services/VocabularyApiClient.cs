// HTTP client for the LexiCall API (FastAPI + MongoDB, api/). Every Try*
// method is best-effort — it never throws, only returns a success flag. The
// local JSON (VocabularyRepository) stays the source of truth; this client
// only pushes a best-effort background sync.
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Services;

public enum ApiConnectionStatus
{
    NotConfigured,
    Unreachable,
    InvalidApiKey,
    Ok
}

// Result of a delta pull: the received records, plus the server timestamp
// (X-Sync-Timestamp) to use as the next updatedSince — never recomputed from
// the local clock, see AppSettings.LastPulledAt.
public sealed record SyncPullResult<T>(IReadOnlyList<T> Items, string? ServerTimestamp);

// NotConfigured/Failed are distinguished (unlike the bool Try* methods below)
// because this call is an explicit user action, not background sync — the
// caller needs to show a meaningful error, not swallow it.
public enum EntryEnrichmentStatus
{
    NotConfigured,
    Failed,
    Ok
}

public sealed record FieldSuggestion<T>(T Value, string? Justification);

// A field absent here (rather than present with a null suggestion) means the
// API judged it locked or already satisfactory — see POST /enrichment/fields
// (response_model_exclude_none=True).
public sealed record EntryEnrichmentSuggestions(
    FieldSuggestion<string>? Definition,
    FieldSuggestion<VocabularyEntryType>? Type,
    FieldSuggestion<List<string>>? Synonyms,
    [property: JsonPropertyName("example_sentences")] FieldSuggestion<List<string>>? ExampleSentences);

// Current field values sent as the request body — not an entry id, since this
// must also work for a brand-new, not-yet-saved draft (see
// EntryEditorWindowViewModel), which has nothing to look up server-side yet.
public sealed record EntryEnrichmentDraft(
    string Word,
    string Definition,
    VocabularyEntryType Type,
    List<string> Synonyms,
    List<string> ExampleSentences,
    List<string> LockedFields);

public sealed class VocabularyApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient? _httpClient;
    // Separate, longer-timeout client for the LLM-backed enrichment calls:
    // those take 2-6s (up to ~6s with the web_search fallback), well past the
    // 2s timeout tuned for silent background sync below.
    private readonly HttpClient? _enrichmentHttpClient;

    public VocabularyApiClient(string? baseUrl, string? apiKey)
    {
        _httpClient = CreateHttpClient(baseUrl, apiKey, TimeSpan.FromSeconds(2));
        _enrichmentHttpClient = CreateHttpClient(baseUrl, apiKey, TimeSpan.FromSeconds(20));
    }

    private static HttpClient? CreateHttpClient(string? baseUrl, string? apiKey, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        HttpClient client;
        try
        {
            client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = timeout
            };
        }
        catch (UriFormatException)
        {
            // Malformed URL in settings: disable sync rather than block
            // app startup.
            return null;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        return client;
    }

    public bool IsConfigured => _httpClient is not null;

    // Used by OptionsWindow's "Test connection" button: distinguishes an
    // unreachable API from an invalid key, for a useful error message.
    public async Task<ApiConnectionStatus> TestConnectionAsync()
    {
        if (_httpClient is null)
        {
            return ApiConnectionStatus.NotConfigured;
        }

        try
        {
            using var healthResponse = await _httpClient.GetAsync("/health").ConfigureAwait(false);

            if (!healthResponse.IsSuccessStatusCode)
            {
                return ApiConnectionStatus.Unreachable;
            }

            // /auth: a plain API key check with no Mongo query (unlike
            // /entries, which would scan the whole collection for a
            // connectivity test that needs no data at all).
            using var authResponse = await _httpClient.GetAsync(
                "/auth", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            return authResponse.StatusCode == HttpStatusCode.Unauthorized
                ? ApiConnectionStatus.InvalidApiKey
                : ApiConnectionStatus.Ok;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return ApiConnectionStatus.Unreachable;
        }
    }

    // The whole entry (Images included, bytes and all) goes in one PUT — the
    // API decides server-side which images to mirror into entry_images (see
    // routers/entries.py), in a single network round trip.
    public Task<bool> TryUpsertEntryAsync(VocabularyEntry entry) =>
        TryUpsertAsync(entry.Id, "/entries", entry);

    // deletedAt is the real local deletion time (not the sync time, which can
    // happen much later if offline) — same principle as UpdatedAt being
    // stamped at edit time, needed so the API's tombstone carries the correct
    // LWW timestamp.
    public Task<bool> TryDeleteEntryAsync(Guid id, DateTimeOffset deletedAt) =>
        TryDeleteAsync($"/entries/{id}?deleted_at={Uri.EscapeDataString(deletedAt.UtcDateTime.ToString("o"))}");

    public Task<bool> TryUpsertCategoryAsync(VocabularyCategory category) =>
        TryUpsertAsync(category.Id, "/categories", category);

    public Task<bool> TryDeleteCategoryAsync(Guid id, DateTimeOffset deletedAt) =>
        TryDeleteAsync($"/categories/{id}?deleted_at={Uri.EscapeDataString(deletedAt.UtcDateTime.ToString("o"))}");

    public Task<SyncPullResult<VocabularyCategory>?> TryPullCategoriesAsync(string? updatedSince) =>
        TryPullAsync<VocabularyCategory>("/categories", updatedSince);

    public Task<SyncPullResult<VocabularyEntry>?> TryPullEntriesAsync(string? updatedSince) =>
        TryPullAsync<VocabularyEntry>("/entries", updatedSince);

    private async Task<SyncPullResult<T>?> TryPullAsync<T>(string resourcePath, string? updatedSince)
    {
        if (_httpClient is null)
        {
            return null;
        }

        var path = updatedSince is null
            ? resourcePath
            : $"{resourcePath}?updated_since={Uri.EscapeDataString(updatedSince)}";

        try
        {
            using var response = await _httpClient.GetAsync(path).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var items = await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions).ConfigureAwait(false) ?? [];
            var serverTimestamp = response.Headers.TryGetValues("X-Sync-Timestamp", out var values)
                ? values.FirstOrDefault()
                : null;
            return new SyncPullResult<T>(items, serverTimestamp);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // PUT is a true upsert server-side (see entries_repo.put_entry/
    // categories_repo.put_category in api/): creates the record if it
    // doesn't exist yet, updates it otherwise. A single call, never a POST
    // fallback — the create-or-update decision belongs entirely to the server.
    private async Task<bool> TryUpsertAsync<T>(Guid id, string resourcePath, T payload)
    {
        if (_httpClient is null)
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.PutAsJsonAsync($"{resourcePath}/{id}", payload, JsonOptions).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
    }

    // Explicit user action (the enrichment buttons on the Détails card and in
    // EntryEditorWindow), not background sync: returns a status instead of a
    // plain bool so the caller can show a meaningful error rather than
    // swallow the failure. ErrorDetail carries the real cause (FastAPI's
    // {"detail": "..."} body, or the exception message) so the UI isn't stuck
    // with one generic string.
    public async Task<(EntryEnrichmentStatus Status, EntryEnrichmentSuggestions? Suggestions, string? ErrorDetail)> TrySuggestEntryEnrichmentAsync(EntryEnrichmentDraft draft)
    {
        if (_enrichmentHttpClient is null)
        {
            return (EntryEnrichmentStatus.NotConfigured, null, null);
        }

        try
        {
            using var response = await _enrichmentHttpClient
                .PostAsJsonAsync("/enrichment/fields", draft, JsonOptions)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = await ReadErrorDetailAsync(response).ConfigureAwait(false);
                return (EntryEnrichmentStatus.Failed, null, errorDetail);
            }

            var result = await response.Content.ReadFromJsonAsync<EntryEnrichmentSuggestions>(JsonOptions).ConfigureAwait(false);
            return result is null
                ? (EntryEnrichmentStatus.Failed, null, null)
                : (EntryEnrichmentStatus.Ok, result, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (EntryEnrichmentStatus.Failed, null, ex.Message);
        }
    }

    // FastAPI's default error shape is {"detail": "..."} (HTTPException) or
    // {"detail": [...]} (422 validation errors) — fall back to the raw body,
    // then to the bare status code, for anything else (e.g. a proxy error page).
    private static async Task<string?> ReadErrorDetailAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"HTTP {(int)response.StatusCode}";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON — fall through to the raw body.
        }

        return body.Length > 300 ? body[..300] : body;
    }

    private async Task<bool> TryDeleteAsync(string resourcePath)
    {
        if (_httpClient is null)
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.DeleteAsync(resourcePath).ConfigureAwait(false);
            // 404 = never synced: nothing to delete server-side, not a
            // failure from the caller's point of view.
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
