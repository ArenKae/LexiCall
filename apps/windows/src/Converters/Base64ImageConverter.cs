// Converts an entry's stored base64 string into a displayable BitmapImage.
// An empty or invalid string yields null (also used to drive visibility).
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

    // Exposed as static so it can be reused outside a binding (e.g. ImagePreviewWindow).
    public static BitmapImage? ToBitmapImage(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var bytes = System.Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
            return null;
        }
    }
}
