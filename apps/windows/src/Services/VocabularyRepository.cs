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

        var json = File.ReadAllText(_filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new VocabularyDatabase();
        }

        if (JsonNode.Parse(json) is not JsonObject root)
        {
            return new VocabularyDatabase();
        }

        MigrateLegacyClientLastWrite(root);

        return SanitizeDatabase(
            JsonSerializer.Deserialize<VocabularyDatabase>(root.ToJsonString(), JsonOptions) ?? new VocabularyDatabase());
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

        var json = JsonSerializer.Serialize(database, JsonOptions);
        File.WriteAllText(_filePath, json);
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
