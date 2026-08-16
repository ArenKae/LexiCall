// Converts a VocabularyEntryType into its displayed French label.
using System.Globalization;
using System.Windows.Data;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.Converters;

public sealed class VocabularyEntryTypeToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is VocabularyEntryType type ? VocabularyEntryTypeCatalog.GetLabel(type) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
