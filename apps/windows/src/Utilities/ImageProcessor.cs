// Prepares a user-picked image before it's stored inline in vocabulary.json:
// downscale to a max dimension, then JPEG compression, so the JSON database
// stays light even after many images are added.
using System.IO;
using System.Windows.Media.Imaging;

namespace LexiCall.Desktop.Utilities;

public static class ImageProcessor
{
    private const int MaxDimensionPixels = 1024;
    private const int JpegQualityLevel = 80;

    public static bool TryEncodeImage(string filePath, out string base64Image, out string error)
    {
        base64Image = string.Empty;
        error = string.Empty;

        try
        {
            using var fileStream = File.OpenRead(filePath);
            var decoder = BitmapDecoder.Create(
                fileStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            BitmapSource source = frame;
            var largestSide = Math.Max(frame.PixelWidth, frame.PixelHeight);

            if (largestSide > MaxDimensionPixels)
            {
                var scale = (double)MaxDimensionPixels / largestSide;
                source = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
            }

            var encoder = new JpegBitmapEncoder { QualityLevel = JpegQualityLevel };
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var memoryStream = new MemoryStream();
            encoder.Save(memoryStream);

            base64Image = Convert.ToBase64String(memoryStream.ToArray());
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = "Impossible de lire ce fichier comme une image.";
            return false;
        }
    }
}
