// Resolves a CategoryIconOption.IconKey (e.g. "Solar.dog") to its
// PathGeometry resource ("Icon." + key, see Themes/Icons.Generated.xaml).
// Returns null for anything else -- including a legacy emoji character left
// over from before the vector icon set, so the XAML can fall back to
// rendering that string as text instead.
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LexiCall.Desktop.Converters;

public sealed class IconKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrEmpty(key))
        {
            return null;
        }

        return Application.Current.TryFindResource("Icon." + key) as Geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
