// True when every AI-enrichment-lockable field of an entry is locked: lets the
// entry list card style its lock toggle without a ViewModel per row.
using System.Globalization;
using System.Windows.Data;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.Converters;

public sealed class EntryLockedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is VocabularyEntry entry && EntryLocks.IsFullyLocked(entry);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
