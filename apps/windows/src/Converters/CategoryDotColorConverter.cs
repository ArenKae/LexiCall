// Fully opaque version of a category's effective color, for the small color
// dot next to its name (entry list, detail panel, category tree). Unlike
// CategoryChipColorConverter's translucent chip background, a small dot needs
// to stay saturated to read clearly. MultiBinding:
// [0] = the VocabularyCategory, [1] = all known categories.
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.Converters;

public sealed class CategoryDotColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [VocabularyCategory category, IEnumerable<VocabularyCategory> categoriesValue])
        {
            return Brushes.Transparent;
        }

        var categories = categoriesValue as IReadOnlyCollection<VocabularyCategory> ?? categoriesValue.ToList();
        var colorIndexes = CategoryHierarchy.ComputeColorIndexes(categories);
        var colorOverrides = CategoryColorStore.LoadAll();
        var color = CategoryColorResolver.Resolve(category, categories, colorIndexes, colorOverrides);

        return new SolidColorBrush(color);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
