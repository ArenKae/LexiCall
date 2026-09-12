// Converts an entry's stored base64 string — or raw bytes downloaded from the
// API — into a displayable BitmapImage. Empty or invalid input yields null
// (also used to drive visibility).
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LexiCall.Desktop.Converters;

public sealed class Base64ImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string base64 ? ToBitmapImage(base64) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // Exposed as static so it can be reused outside a binding (e.g. MainWindowViewModel).
    public static BitmapImage? ToBitmapImage(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return ToBitmapImage(System.Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // Same decode for image bytes downloaded from the API, which never went
    // through base64 at all.
    public static BitmapImage? ToBitmapImage(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            // Frozen so it can be handed to the UI thread from whatever
            // thread the download resumed on.
            image.Freeze();

            return image;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
