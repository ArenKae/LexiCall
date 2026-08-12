// Raw read/write for sync_history.json — own file, separate from
// settings.json (which is load-merge-saved by several unrelated services;
// this is append-mostly operational data, not a preference).
using System.IO;
using System.Text.Json;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Services;

internal static class SyncHistoryStore
{
    public const int MaxEntries = 200;

    private static readonly string HistoryFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LexiCall",
        "sync_history.json");

    public static List<SyncHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(HistoryFilePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<SyncHistoryEntry>>(File.ReadAllText(HistoryFilePath))
                ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // A corrupted history file must never block startup.
            return [];
        }
    }

    // Entries are expected newest-first; trims to MaxEntries here so every
    // caller gets the cap for free.
    public static void Save(IReadOnlyList<SyncHistoryEntry> entries)
    {
        try
        {
            var toSave = entries.Count > MaxEntries ? entries.Take(MaxEntries).ToList() : entries;
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryFilePath)!);
            File.WriteAllText(HistoryFilePath, JsonSerializer.Serialize(toSave));
        }
        catch (IOException)
        {
            // Not saved: harmless, history stays active in memory for the session.
        }
    }
}
