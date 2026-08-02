// Turns a category's hierarchy depth into a left margin, to indent flat
// lists (entry editor's category checklist) without ViewModels touching any
// WPF type.
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
