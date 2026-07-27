// Ordre d'affichage des catégories choisi manuellement (menu contextuel
// "Monter"/"Descendre") : une préférence de présentation locale à l'app
// Windows (comme les couleurs, voir CategoryColorStore), stockée dans
// settings.json — jamais dans vocabulary.json ni synchronisée vers api/.
namespace LexiCall.Desktop.Services;

public static class CategoryOrderStore
{
    public static IReadOnlyDictionary<Guid, int> LoadAll() => SettingsStore.Load().CategoryOrder;

    // Fige l'ordre d'un groupe de frères : leur assigne des rangs 0..N-1 dans
    // l'ordre donné, remplaçant tout rang déjà stocké pour ces catégories.
    public static void SetOrder(IEnumerable<Guid> orderedCategoryIds)
    {
        var settings = SettingsStore.Load();
        var rank = 0;

        foreach (var categoryId in orderedCategoryIds)
        {
            settings.CategoryOrder[categoryId] = rank++;
        }

        SettingsStore.Save(settings);
    }

    public static void ClearOrder(Guid categoryId)
    {
        var settings = SettingsStore.Load();

        if (settings.CategoryOrder.Remove(categoryId))
        {
            SettingsStore.Save(settings);
        }
    }
}
