// Client HTTP vers l'API LexiCall (FastAPI + MongoDB, api/). Chaque méthode
// Try* est best-effort : elle ne lève jamais, retourne juste un indicateur de
// succès — le JSON local (VocabularyRepository) reste la source de vérité,
// ce client ne fait que pousser une synchronisation en tâche de fond.
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Services;

public enum ApiConnectionStatus
{
    NotConfigured,
    Unreachable,
    InvalidApiKey,
    Ok
}

// Résultat d'un pull différentiel : les enregistrements reçus, et
// l'horodatage serveur (X-Sync-Timestamp) à utiliser comme prochain
// updatedSince — jamais recalculé depuis l'horloge locale, voir
// AppSettings.LastPulledAt.
public sealed record SyncPullResult<T>(IReadOnlyList<T> Items, string? ServerTimestamp);

public sealed class VocabularyApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient? _httpClient;

    public VocabularyApiClient(string? baseUrl, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        try
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(2)
            };
        }
        catch (UriFormatException)
        {
            // URL mal formée dans les réglages : synchro désactivée plutôt
            // que de bloquer le démarrage de l'application.
            _httpClient = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    public bool IsConfigured => _httpClient is not null;

    // Utilisé par le bouton « Tester la connexion » d'OptionsWindow : distingue
    // une API injoignable d'une clé invalide, pour un message d'erreur utile.
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

            // /auth : simple vérification de la clé API, sans requête Mongo
            // (contrairement à /entries, qui scannerait toute la collection
            // pour un test de connexion qui n'a besoin d'aucune donnée).
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

    // L'entrée entière (ImageBase64 compris) part dans le même PUT/POST :
    // c'est l'API qui décide côté serveur de répercuter ou non l'image dans
    // entry_images (voir routers/entries.py), en un seul aller-retour réseau.
    public Task<bool> TryUpsertEntryAsync(VocabularyEntry entry) =>
        TryUpsertAsync(entry.Id, "/entries", entry);

    // deletedAt : heure réelle de la suppression locale (pas de la synchro,
    // qui peut survenir bien plus tard si hors-ligne) — même principe que
    // l'UpdatedAt déjà tamponné à l'édition, nécessaire pour que le tombstone
    // côté API porte le bon horodatage LWW.
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

    // PUT fait un vrai upsert côté serveur (voir entries_repo.put_entry/
    // categories_repo.put_category côté api/) : crée l'enregistrement s'il
    // n'existe pas encore, le met à jour sinon (avec le même Id des deux
    // côtés, l'API respecte l'Id fourni dans l'URL). Un seul appel, jamais
    // de repli sur POST — la décision revient entièrement au serveur.
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

    private async Task<bool> TryDeleteAsync(string resourcePath)
    {
        if (_httpClient is null)
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.DeleteAsync(resourcePath).ConfigureAwait(false);
            // 404 = jamais synchronisée : rien à supprimer côté serveur, ce
            // n'est pas un échec du point de vue de l'appelant.
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
