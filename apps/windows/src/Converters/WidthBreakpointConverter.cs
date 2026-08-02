// Detects when the detail panel drops below the width where it switches from
// a side-by-side layout (metadata | image) to a stacked one. Used via a
// DataTrigger on the container Grid's ActualWidth.
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
