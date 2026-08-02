// A category's effective color: a manually chosen color (see
// CategoryColorStore) on itself or an ancestor if one exists, otherwise the
// automatic hue derived from its root's index (golden-angle spacing, for
// well-separated hues at any category count). Single entry point for the
// tree (MainWindowViewModel.RebuildCategoryTree), category chips
// (CategoryChipColorConverter), and the color picker preview
// (CategoryEditorWindowViewModel).
using System.Windows.Media;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

public static class CategoryColorResolver
{
    private const double GoldenAngle = 137.508;
    private const double Saturation = 0.5;
    private const double Lightness = 0.52;

    public static Color Resolve(
        VocabularyCategory category,
        IReadOnlyCollection<VocabularyCategory> allCategories,
        IReadOnlyDictionary<Guid, int> colorIndexes,
        IReadOnlyDictionary<Guid, string> colorOverrides)
    {
        var categoriesById = allCategories.ToDictionary(c => c.Id);
        var current = category;
        var visited = new HashSet<Guid>();

        while (current is not null && visited.Add(current.Id))
        {
            if (colorOverrides.TryGetValue(current.Id, out var hex) && TryParseHex(hex, out var overrideColor))
            {
                return overrideColor;
            }

            current = current.ParentId is Guid parentId && categoriesById.TryGetValue(parentId, out var parent)
                ? parent
                : null;
        }

        return FromIndex(colorIndexes.GetValueOrDefault(category.Id));
    }

    public static Color FromIndex(int index)
    {
        var hue = index * GoldenAngle % 360;
        return HslToColor(hue, Saturation, Lightness);
    }

    public static bool TryParseHex(string hex, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch (FormatException)
        {
            color = default;
            return false;
        }
    }

    public static Color HslToColor(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var huePrime = hue / 60;
        var secondary = chroma * (1 - Math.Abs(huePrime % 2 - 1));

        var (r1, g1, b1) = huePrime switch
        {
            < 1 => (chroma, secondary, 0.0),
            < 2 => (secondary, chroma, 0.0),
            < 3 => (0.0, chroma, secondary),
            < 4 => (0.0, secondary, chroma),
            < 5 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary)
        };

        var lightnessMatch = lightness - chroma / 2;

        return Color.FromRgb(
            (byte)Math.Round((r1 + lightnessMatch) * 255),
            (byte)Math.Round((g1 + lightnessMatch) * 255),
            (byte)Math.Round((b1 + lightnessMatch) * 255));
    }
}
