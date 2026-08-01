// Client HTTP vers l'API LexiCall (FastAPI + MongoDB, api/). Chaque méthode
// Try* est best-effort : elle ne lève jamais, retourne juste un indicateur de
// succès — le JSON local (VocabularyRepository) reste la source de vérité,
// ce client ne fait que pousser une synchronisation en tâche de fond.
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    // L'image n'est plus envoyée dans le JSON de l'entrée (voir api/ :
    // entry_images est une collection Mongo séparée, pour qu'un scan de
    // `entries` ne charge plus jamais de blobs d'image) : on la retire du
    // payload puis on la pousse à part via /entries/{id}/image.
    public async Task<bool> TryUpsertEntryAsync(VocabularyEntry entry)
    {
        var payload = JsonSerializer.SerializeToNode(entry, JsonOptions)!.AsObject();
        payload.Remove(nameof(VocabularyEntry.ImageBase64));

        var entryOk = await TryUpsertAsync(entry.Id, "/entries", payload).ConfigureAwait(false);
        if (!entryOk)
        {
            return false;
        }

        return await TryUpsertEntryImageAsync(entry.Id, entry.ImageBase64).ConfigureAwait(false);
    }

    // deletedAt : heure réelle de la suppression locale (pas de la synchro,
    // qui peut survenir bien plus tard si hors-ligne) — même principe que
    // l'UpdatedAt déjà tamponné à l'édition, nécessaire pour que le tombstone
    // côté API porte le bon horodatage LWW.
    public Task<bool> TryDeleteEntryAsync(Guid id, DateTimeOffset deletedAt) =>
        TryDeleteAsync($"/entries/{id}?deleted_at={Uri.EscapeDataString(deletedAt.UtcDateTime.ToString("o"))}");

    // Chaîne vide = pas d'image : supprime la ressource côté serveur plutôt
    // que d'envoyer un PUT vide. La suppression d'image en cascade sur
    // suppression d'entrée est gérée côté serveur (DELETE /entries/{id}),
    // aucun appel supplémentaire n'est nécessaire depuis TryDeleteEntryAsync.
    private async Task<bool> TryUpsertEntryImageAsync(Guid entryId, string imageBase64)
    {
        if (_httpClient is null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(imageBase64))
        {
            return await TryDeleteAsync($"/entries/{entryId}/image").ConfigureAwait(false);
        }

        try
        {
            var imageBytes = Convert.FromBase64String(imageBase64);
            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            using var response = await _httpClient.PutAsync($"/entries/{entryId}/image", content).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            return false;
        }
    }

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

    // PUT d'abord (mise à jour) ; si 404 (jamais synchronisée avant), bascule
    // en POST avec le même Id local — l'API préserve un Id fourni par le
    // client à la création (voir VocabularyEntryCreate/VocabularyCategoryCreate
    // côté api/), donc l'entrée garde le même Id des deux côtés.
    private async Task<bool> TryUpsertAsync<T>(Guid id, string resourcePath, T payload)
    {
        if (_httpClient is null)
        {
            return false;
        }

        try
        {
            using var putResponse = await _httpClient.PutAsJsonAsync($"{resourcePath}/{id}", payload, JsonOptions).ConfigureAwait(false);

            if (putResponse.IsSuccessStatusCode)
            {
                return true;
            }

            if (putResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return false;
            }

            using var postResponse = await _httpClient.PostAsJsonAsync(resourcePath, payload, JsonOptions).ConfigureAwait(false);
            return postResponse.IsSuccessStatusCode;
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
