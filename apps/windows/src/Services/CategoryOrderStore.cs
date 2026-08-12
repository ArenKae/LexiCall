// Manually assigned category display order (context menu "Monter"/"Descendre"
// — Move up/down): a local presentation preference (like colors, see
// CategoryColorStore), stored in settings.json — never in vocabulary.json or
// synced to api/.
namespace LexiCall.Desktop.Services;

public static class CategoryOrderStore
{
    public static IReadOnlyDictionary<Guid, int> LoadAll() => SettingsStore.Load().CategoryOrder;

    // Fixes the order of a sibling group: assigns ranks 0..N-1 in the given
    // order, replacing any rank already stored for these categories.
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
