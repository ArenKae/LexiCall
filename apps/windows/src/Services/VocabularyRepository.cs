// Local persistence layer: loads/saves the whole database as one JSON file at
// %LOCALAPPDATA%\LexiCall\vocabulary.json.
using System.IO;
using System.Text.Json;
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

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new VocabularyDatabase();
        }

        return SanitizeDatabase(
            JsonSerializer.Deserialize<VocabularyDatabase>(json, JsonOptions) ?? new VocabularyDatabase());
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
