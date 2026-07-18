// Service responsable de la persistance locale.
// Pour la Phase 1, LexiCall sauvegarde toute sa base dans un unique fichier JSON
// placé dans %LOCALAPPDATA%\LexiCall\vocabulary.json.
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

        // Les premières versions sauvegardaient directement un tableau d'entrées.
        // Ce switch conserve la compatibilité en migrant ce format vers le
        // nouveau document racine { Entries, Categories }.
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => MigrateLegacyDatabase(json),
            JsonValueKind.Object => SanitizeDatabase(
                JsonSerializer.Deserialize<VocabularyDatabase>(json, JsonOptions) ?? new VocabularyDatabase()),
            _ => new VocabularyDatabase()
        };
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

    private static VocabularyDatabase MigrateLegacyDatabase(string json)
    {
        var legacyEntries = JsonSerializer.Deserialize<List<LegacyVocabularyEntry>>(json, JsonOptions) ?? [];
        var categoriesByName = new Dictionary<string, VocabularyCategory>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<VocabularyEntry>();

        foreach (var legacyEntry in legacyEntries)
        {
            var categoryIds = new List<Guid>();

            foreach (var categoryName in legacyEntry.Categories.Select(category => category.Trim()))
            {
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    continue;
                }

                if (!categoriesByName.TryGetValue(categoryName, out var category))
                {
                    category = new VocabularyCategory
                    {
                        Name = categoryName
                    };
                    categoriesByName[categoryName] = category;
                }

                categoryIds.Add(category.Id);
            }

            entries.Add(new VocabularyEntry
            {
                Id = legacyEntry.Id == Guid.Empty ? Guid.NewGuid() : legacyEntry.Id,
                Word = legacyEntry.Word,
                Definition = legacyEntry.Definition,
                Synonyms = legacyEntry.Synonyms,
                ExampleSentences = legacyEntry.ExampleSentences,
                Notes = legacyEntry.Notes,
                Source = legacyEntry.Source,
                CategoryIds = categoryIds.Distinct().ToList(),
                Tags = legacyEntry.Tags,
                CreatedAt = legacyEntry.CreatedAt,
                UpdatedAt = legacyEntry.UpdatedAt
            });
        }

        return SanitizeDatabase(new VocabularyDatabase
        {
            Entries = entries,
            Categories = categoriesByName.Values.ToList()
        });
    }

    private static VocabularyDatabase SanitizeDatabase(VocabularyDatabase database)
    {
        // Si une catégorie a été supprimée ou si le JSON est incohérent, on retire
        // les références orphelines côté entrées pour éviter des erreurs d'affichage.
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

    private sealed class LegacyVocabularyEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public string Word { get; init; } = string.Empty;

        public string Definition { get; init; } = string.Empty;

        public List<string> Synonyms { get; init; } = [];

        public List<string> ExampleSentences { get; init; } = [];

        public string Notes { get; init; } = string.Empty;

        public string Source { get; init; } = string.Empty;

        public List<string> Categories { get; init; } = [];

        public List<string> Tags { get; init; } = [];

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    }
}
