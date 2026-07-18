// Lecture/écriture brute de settings.json. Toujours charger-fusionner-sauvegarder
// plutôt qu'écraser : ThemeService et WindowLayoutService se partagent le même
// fichier, chacun ne modifiant que ses propres champs.
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
            // Un fichier de préférences corrompu ne doit jamais empêcher le démarrage.
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
            // Préférence non sauvegardée : sans gravité, elle reste active pour la session.
        }
    }
}
