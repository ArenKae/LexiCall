// On-disk cache for entry image bytes (%LOCALAPPDATA%\LexiCall\image-cache),
// keyed by image Id. A pull carries metadata only, so bytes are downloaded
// once and kept here instead of back in vocabulary.json.
using System.Collections.Concurrent;
using System.IO;

namespace LexiCall.Desktop.Services;

internal static class EntryImageCache
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LexiCall",
        "image-cache");

    // Dedupes concurrent downloads of the same image — re-selecting an entry
    // while its thumbnails are still loading would otherwise fire a second set.
    private static readonly ConcurrentDictionary<Guid, Task<byte[]?>> InFlight = new();

    // An image Id is never reused for different bytes (the picker stamps a
    // fresh Guid on every added image, and never edits one in place), so a
    // cached file never needs invalidating.
    public static byte[]? TryReadCached(Guid imageId)
    {
        try
        {
            var path = GetCachePath(imageId);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // Never throws: a failed download returns null, and a later call retries
    // it (nothing negative is cached).
    public static Task<byte[]?> LoadAsync(VocabularyApiClient client, Guid entryId, Guid imageId)
    {
        if (TryReadCached(imageId) is { } cached)
        {
            return Task.FromResult<byte[]?>(cached);
        }

        // Guarded so the download task always yields before completing:
        // an unconfigured client would return synchronously and leave its
        // finished task parked in InFlight, defeating every later retry.
        return client.IsConfigured
            ? InFlight.GetOrAdd(imageId, id => DownloadAsync(client, entryId, id))
            : Task.FromResult<byte[]?>(null);
    }

    private static async Task<byte[]?> DownloadAsync(VocabularyApiClient client, Guid entryId, Guid imageId)
    {
        try
        {
            var bytes = await client.TryGetEntryImageAsync(entryId, imageId).ConfigureAwait(false);

            if (bytes is { Length: > 0 })
            {
                Write(imageId, bytes);
                return bytes;
            }

            return null;
        }
        finally
        {
            InFlight.TryRemove(imageId, out _);
        }
    }

    private static void Write(Guid imageId, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllBytes(GetCachePath(imageId), bytes);
        }
        catch (IOException)
        {
            // Not cached: the image still displays, it just gets re-downloaded
            // the next time the entry is opened.
        }
    }

    private static string GetCachePath(Guid imageId) =>
        Path.Combine(CacheDirectory, $"{imageId}.jpg");
}
