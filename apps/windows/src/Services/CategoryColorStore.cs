// Manually assigned category colors: a local presentation preference (like
// theme or column layout), stored in settings.json — never in vocabulary.json
// or synced to api/. See CategoryColorResolver for the actual resolution
// (override here, otherwise an automatic hue derived from the hierarchy).
namespace LexiCall.Desktop.Services;

public static class CategoryColorStore
{
    public static IReadOnlyDictionary<Guid, string> LoadAll() => SettingsStore.Load().CategoryColors;

    public static void SetColor(Guid categoryId, string hexColor)
    {
        var settings = SettingsStore.Load();
        settings.CategoryColors[categoryId] = hexColor;
        SettingsStore.Save(settings);
    }

    public static void ClearColor(Guid categoryId)
    {
        var settings = SettingsStore.Load();

        if (settings.CategoryColors.Remove(categoryId))
        {
            SettingsStore.Save(settings);
        }
    }
}
