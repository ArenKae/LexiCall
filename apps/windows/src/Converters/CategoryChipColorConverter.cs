// Couleur de fond des pills de catégorie sur une entrée : la couleur effective
// de la catégorie (CategoryColorResolver — choisie manuellement ou dérivée
// automatiquement), mêlée en transparence à ce qu'il y a derrière — comme le
// halo de couleur de l'arbre — plutôt qu'en aplat plein. Ça garde le lien
// visuel direct avec la couleur de la catégorie sans écart de vivacité entre
// l'arbre et les pills, et reste lisible dans les deux thèmes avec un texte de
// couleur fixe (Brush.Text.Primary, posé directement en XAML). MultiBinding :
// [0] = VocabularyCategory du chip, [1] = toutes les catégories connues.
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
