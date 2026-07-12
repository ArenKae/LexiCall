// Transforme la profondeur hiérarchique d'une catégorie en marge gauche,
// pour indenter les listes plates (cases à cocher de l'éditeur d'entrée)
// sans que les ViewModels manipulent de types WPF.
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LexiCall.Desktop.Converters;

public sealed class DepthToIndentConverter : IValueConverter
{
    private const double IndentPerLevel = 18;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var depth = value is int depthValue ? depthValue : 0;
        return new Thickness(depth * IndentPerLevel, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
