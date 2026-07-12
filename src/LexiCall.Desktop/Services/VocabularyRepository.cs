using System.IO;
using System.Text.Json;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Services;

public sealed class VocabularyRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public VocabularyRepository(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultFilePath();
    }

    public string FilePath => _filePath;

    public bool DataFileExists => File.Exists(_filePath);

    public IReadOnlyList<VocabularyEntry> LoadEntries()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = File.ReadAllText(_filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<VocabularyEntry>>(json, JsonOptions) ?? [];
    }

    public void SaveEntries(IEnumerable<VocabularyEntry> entries)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(_filePath, json);
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
