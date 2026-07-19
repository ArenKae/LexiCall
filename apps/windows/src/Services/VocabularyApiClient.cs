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

            // ResponseHeadersRead : on n'a besoin que du code de statut, pas
            // de télécharger toutes les entrées pour un simple test de connexion.
            using var entriesResponse = await _httpClient.GetAsync(
                "/entries", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            return entriesResponse.StatusCode == HttpStatusCode.Unauthorized
                ? ApiConnectionStatus.InvalidApiKey
                : ApiConnectionStatus.Ok;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return ApiConnectionStatus.Unreachable;
        }
    }

    public Task<bool> TryUpsertEntryAsync(VocabularyEntry entry) =>
        TryUpsertAsync(entry.Id, "/entries", entry);

    public Task<bool> TryDeleteEntryAsync(Guid id) =>
        TryDeleteAsync($"/entries/{id}");

    public Task<bool> TryUpsertCategoryAsync(VocabularyCategory category) =>
        TryUpsertAsync(category.Id, "/categories", category);

    public Task<bool> TryDeleteCategoryAsync(Guid id) =>
        TryDeleteAsync($"/categories/{id}");

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
