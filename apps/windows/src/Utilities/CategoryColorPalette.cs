// Swatch palette offered by ColorPickerWindow: hues evenly spaced at the same
// saturation/lightness as CategoryColorResolver.FromIndex, so a manually
// picked color blends in with the automatic ones.
namespace LexiCall.Desktop.Utilities;

public static class CategoryColorPalette
{
    private const int SwatchCount = 18;

    public static IReadOnlyList<string> Swatches { get; } = BuildSwatches();

    private static IReadOnlyList<string> BuildSwatches()
    {
        var swatches = new List<string>(SwatchCount);

        for (var i = 0; i < SwatchCount; i++)
        {
            var hue = i * 360.0 / SwatchCount;
            var color = CategoryColorResolver.HslToColor(hue, saturation: 0.5, lightness: 0.52);
            swatches.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        return swatches;
    }
}
