// Converts an entry's CategoryIds into displayable category chips.
// MultiBinding: [0] = the entry's List<Guid>, [1] = all known categories.
// Returns the categories themselves, not just their names, so chips carry
// the Id needed for their click handler (selection in the sidebar).
using System.Globalization;
using System.Windows.Data;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Converters;

public sealed class CategoryNamesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [IEnumerable<Guid> categoryIds, IEnumerable<VocabularyCategory> categories])
        {
            return Array.Empty<VocabularyCategory>();
        }

        var categoriesById = categories.ToDictionary(category => category.Id);

        return categoryIds
            .Where(categoriesById.ContainsKey)
            .Select(categoryId => categoriesById[categoryId])
            .ToList();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
