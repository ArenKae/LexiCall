// Local persistence layer: loads/saves the whole database as one JSON file at
// %LOCALAPPDATA%\LexiCall\vocabulary.json.

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Services;

public sealed class VocabularyRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;

    public VocabularyRepository(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultFilePath();
    }

    public string FilePath => _filePath;

    public bool DataFileExists => File.Exists(_filePath);

    public VocabularyDatabase LoadDatabase()
    {
        if (!File.Exists(_filePath))
        {
            return new VocabularyDatabase();
        }

        // Parsed and deserialized straight off the stream, never through an
        // intermediate string: the whole file is one allocation well past the
        // 85 KB large-object threshold, and the LOH is never compacted.
        using var stream = File.OpenRead(_filePath);

        if (IsBlank(stream) || JsonNode.Parse(stream) is not JsonObject root)
        {
            return new VocabularyDatabase();
        }

        MigrateLegacyClientLastWrite(root);

        return SanitizeDatabase(root.Deserialize<VocabularyDatabase>(JsonOptions) ?? new VocabularyDatabase());
    }

    // A blank file counts as no file at all. Anything else that fails to parse
    // is left to throw: starting from an empty database would overwrite the
    // unreadable original on the next save.
    private static bool IsBlank(Stream stream)
    {
        int next;

        while ((next = stream.ReadByte()) >= 0)
        {
            if (!char.IsWhiteSpace((char)next))
            {
                stream.Position = 0;
                return false;
            }
        }

        return true;
    }

    // A file saved before the ClientLastWrite/UpdatedAt split still has every
    // record's edit time under the old key "UpdatedAt". Copied across before
    // deserializing so a pre-existing entry doesn't silently fall back to
    // ClientLastWrite's property default (the moment this load runs) and look
    // freshly edited — which would then win every future Last-Write-Wins
    // comparison purely by having the newest possible timestamp.
    private static void MigrateLegacyClientLastWrite(JsonObject root)
    {
        foreach (var collectionKey in new[] { "Entries", "Categories" })
        {
            if (root[collectionKey] is not JsonArray records)
            {
                continue;
            }

            foreach (var record in records.OfType<JsonObject>())
            {
                if (!record.ContainsKey("ClientLastWrite") && record["UpdatedAt"] is { } legacy)
                {
                    record["ClientLastWrite"] = legacy.DeepClone();
                }
            }
        }
    }

    public void SaveDatabase(VocabularyDatabase database)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Written straight to the stream rather than via JsonSerializer.Serialize's
        // string: a multi-MB string lands on the large object heap, which is
        // never compacted, and this runs on every mutation.
        using var stream = File.Create(_filePath);
        JsonSerializer.Serialize(stream, database, JsonOptions);
    }

    public void ExportTo(string destinationPath)
    {
        File.Copy(_filePath, destinationPath, overwrite: true);
    }

    // Copies the file in as-is rather than re-serializing the parsed object:
    // LoadDatabase already handles legacy shapes and sanitization on reload.
    public void ImportFrom(string sourcePath)
    {
        var json = File.ReadAllText(sourcePath);

        if (JsonSerializer.Deserialize<VocabularyDatabase>(json, JsonOptions) is null)
        {
            throw new JsonException("Le fichier ne contient pas une base LexiCall valide.");
        }

        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(_filePath))
        {
            var backupName = $"vocabulary.backup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json";
            File.Copy(_filePath, Path.Combine(directory ?? string.Empty, backupName), overwrite: true);
        }

        File.Copy(sourcePath, _filePath, overwrite: true);
    }

    private static VocabularyDatabase SanitizeDatabase(VocabularyDatabase database)
    {
        // Strip CategoryIds referencing a category that no longer exists
        // (deleted category, or hand-edited/inconsistent JSON).
        var categoryIds = database.Categories
            .Select(category => category.Id)
            .ToHashSet();

        foreach (var entry in database.Entries)
        {
            entry.CategoryIds.RemoveAll(categoryId => !categoryIds.Contains(categoryId));
        }

        return database;
    }

    private static string GetDefaultFilePath()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = AppContext.BaseDirectory;
        }

        return Path.Combine(localApplicationData, "LexiCall", "vocabulary.json");
    }
}
