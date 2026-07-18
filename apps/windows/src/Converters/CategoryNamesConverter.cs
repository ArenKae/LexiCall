// Convertit les CategoryIds d'une entrée en catégories affichables (chips de
// la liste). MultiBinding : [0] = List<Guid> de l'entrée, [1] = collection des
// catégories connues. On retourne les catégories elles-mêmes (pas juste leur
// nom) pour que les chips puissent porter l'Id nécessaire au clic (sélection
// de la catégorie dans le panneau de gauche). La liste filtrée étant
// reconstruite à chaque mutation, les chips se rafraîchissent sans
// notification supplémentaire.
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
