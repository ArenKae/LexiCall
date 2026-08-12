// Raw read/write for settings.json. Always load-merge-save rather than
// overwrite: ThemeService and WindowLayoutService share this file, each only
// touching its own fields.
using System.IO;
using System.Text.Json;

namespace LexiCall.Desktop.Services;

internal static class SettingsStore
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LexiCall",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath))
                ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // A corrupted preferences file must never block startup.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings));
        }
        catch (IOException)
        {
            // Preference not saved: harmless, it stays active for the session.
        }
    }
}
