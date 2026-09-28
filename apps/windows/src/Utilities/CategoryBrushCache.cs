// Frozen brush per category for the colour dots (entry list, detail panel,
// category tree). Resolving one dot means walking the whole hierarchy and
// reading the colour overrides out of settings.json, and the list renders
// hundreds of dots per pass — so it is done once here and reused.
using System.Windows.Media;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.Utilities;

public static class CategoryBrushCache
{
    private static readonly Dictionary<Guid, SolidColorBrush> Brushes = [];

    private static IReadOnlyDictionary<Guid, int> _colorIndexes = new Dictionary<Guid, int>();
    private static IReadOnlyDictionary<Guid, string> _colorOverrides = new Dictionary<Guid, string>();
    private static bool _isStale = true;

    // Must be called whenever a category is added, removed or reparented, or
    // a colour override changes: nothing else tells the cache it went out of
    // date. UI thread only, like every caller.
    public static void Invalidate() => _isStale = true;

    public static SolidColorBrush GetBrush(
        VocabularyCategory category,
        IReadOnlyCollection<VocabularyCategory> allCategories)
    {
        if (_isStale)
        {
            _isStale = false;
            Brushes.Clear();
            _colorIndexes = CategoryHierarchy.ComputeColorIndexes(allCategories);
            _colorOverrides = CategoryColorStore.LoadAll();
        }

        if (!Brushes.TryGetValue(category.Id, out var brush))
        {
            brush = new SolidColorBrush(
                CategoryColorResolver.Resolve(category, allCategories, _colorIndexes, _colorOverrides));
            // Frozen: a dot's brush is shared across every row that shows it.
            brush.Freeze();
            Brushes[category.Id] = brush;
        }

        return brush;
    }
}
