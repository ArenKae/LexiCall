// Convertit les CategoryIds d'une entrée en noms affichables (chips de la
// liste). MultiBinding : [0] = List<Guid> de l'entrée, [1] = collection des
// catégories connues. La liste filtrée étant reconstruite à chaque mutation,
// les chips se rafraîchissent sans notification supplémentaire.
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
            return Array.Empty<string>();
        }

        var namesById = categories.ToDictionary(category => category.Id, category => category.Name);

        return categoryIds
            .Where(namesById.ContainsKey)
            .Select(categoryId => namesById[categoryId])
            .ToList();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
