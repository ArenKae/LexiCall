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
    // Stored images are up to 1024px on their longest side, but every binding
    // that goes through this converter is a ~120px thumbnail. Decoding at full
    // size costs ~4 MB of unmanaged imaging memory per image against ~260 KB
    // here, and the GC barely sees that cost so it reclaims it late.
    public const int ThumbnailDecodePixels = 256;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string base64 ? ToBitmapImage(base64, ThumbnailDecodePixels) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // Exposed as static so it can be reused outside a binding (e.g. MainWindowViewModel).
    // decodePixels caps the longest decoded side; 0 decodes at full size.
    public static BitmapImage? ToBitmapImage(string base64, int decodePixels = 0)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return ToBitmapImage(System.Convert.FromBase64String(base64), decodePixels);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // Same decode for image bytes downloaded from the API, which never went
    // through base64 at all.
    public static BitmapImage? ToBitmapImage(byte[] bytes, int decodePixels = 0)
    {
        try
        {
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;

            // Exactly one axis is set, on the longest side: setting both would
            // stretch a non-square image, and setting the wrong one would
            // upscale a shape that is already narrow on that axis.
            if (decodePixels > 0 &&
                TryReadPixelSize(bytes, out var width, out var height) &&
                Math.Max(width, height) > decodePixels)
            {
                if (width >= height)
                {
                    image.DecodePixelWidth = decodePixels;
                }
                else
                {
                    image.DecodePixelHeight = decodePixels;
                }
            }

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

    // DelayCreation reads the header only — the pixels are never decoded here.
    private static bool TryReadPixelSize(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            using var stream = new MemoryStream(bytes);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            width = frame.PixelWidth;
            height = frame.PixelHeight;

            return width > 0 && height > 0;
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or FileFormatException)
        {
            return false;
        }
    }
}
