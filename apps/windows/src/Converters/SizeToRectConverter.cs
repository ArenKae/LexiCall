// Builds the Rect a Border needs to clip a child to its own rounded-corner
// shape. Border.CornerRadius only affects how the Border paints its own
// Background/BorderBrush — a child's default clipping (ClipToBounds) is
// always a plain rectangle, so an Image inside a rounded Border still shows
// square corners unless the Border's own Clip is set to a matching rounded
// RectangleGeometry. [0] = ActualWidth, [1] = ActualHeight.
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LexiCall.Desktop.Converters;

public sealed class SizeToRectConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double width, double height])
        {
            return new Rect(0, 0, 0, 0);
        }

        return new Rect(0, 0, Math.Max(0, width), Math.Max(0, height));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
