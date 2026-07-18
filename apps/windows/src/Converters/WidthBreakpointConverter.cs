// Détecte le passage sous le seuil de largeur où le panneau Détails bascule
// d'une disposition côte à côte (métadonnées | image) vers une disposition
// empilée. Utilisé via DataTrigger sur l'ActualWidth du Grid conteneur.
using System.Globalization;
using System.Windows.Data;

namespace LexiCall.Desktop.Converters;

public sealed class WidthBreakpointConverter : IValueConverter
{
    private const double NarrowThreshold = 400;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double width && width < NarrowThreshold;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
