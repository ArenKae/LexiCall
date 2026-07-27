// Couleurs de catégorie choisies manuellement : une préférence de présentation
// locale à l'app Windows (comme le thème ou la disposition des colonnes),
// stockée dans settings.json — jamais dans vocabulary.json ni synchronisée
// vers api/. Voir CategoryColorResolver pour la résolution effective
// (override ici, sinon teinte automatique dérivée de la hiérarchie).
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
