// Attribue une couleur distincte à chaque catégorie racine à partir de son
// ColorIndex. Incrément par angle d'or (137.508°) pour des teintes bien
// séparées quel que soit le nombre de catégories. Teinte fixe, indépendante
// du thème clair/sombre : pas de recalcul au changement de thème.
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LexiCall.Desktop.Converters;

public sealed class CategoryColorConverter : IValueConverter
{
    private const double GoldenAngle = 137.508;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var index = value is int colorIndex ? colorIndex : 0;
        var hue = index * GoldenAngle % 360;
        return new SolidColorBrush(HslToColor(hue, saturation: 0.5, lightness: 0.52));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Color HslToColor(double hue, double saturation, double lightness)
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
