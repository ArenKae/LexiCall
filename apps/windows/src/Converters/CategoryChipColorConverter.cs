// Background color for an entry's category chips: the category's effective
// color (CategoryColorResolver), blended with transparency rather than a
// solid fill, so it reads consistently in both themes against the fixed text
// color (Brush.Text.Primary, set directly in XAML). MultiBinding:
// [0] = the chip's VocabularyCategory, [1] = all known categories.
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.Converters;

public sealed class CategoryChipColorConverter : IMultiValueConverter
{
    private const byte BackgroundAlpha = 130;

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

        return new SolidColorBrush(Color.FromArgb(BackgroundAlpha, color.R, color.G, color.B));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
