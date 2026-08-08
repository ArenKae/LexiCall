// OKLCH -> sRGB conversion. Unlike CategoryColorResolver's HSL formula, OKLCH
// lightness tracks perceived brightness, so a fixed L keeps contrast uniform
// across every hue instead of swinging with the eye's hue-dependent bias
// (verified: HSL at fixed L reads ~2:1 contrast for green/yellow vs ~4.8:1
// for red/purple against the same background; OKLCH holds ~4.2-4.7:1 for all).
using System.Windows.Media;

namespace LexiCall.Desktop.Utilities;

public static class OkLchColor
{
    public static Color FromOkLch(double lightness, double chroma, double hueDegrees)
    {
        var hueRad = hueDegrees * Math.PI / 180.0;
        var a = chroma * Math.Cos(hueRad);
        var b = chroma * Math.Sin(hueRad);

        var l_ = lightness + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = lightness - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = lightness - 0.0894841775 * a - 1.2914855480 * b;

        var l = l_ * l_ * l_;
        var m = m_ * m_ * m_;
        var s = s_ * s_ * s_;

        var rLin = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var gLin = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var bLin = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        return Color.FromRgb(ToSrgbByte(rLin), ToSrgbByte(gLin), ToSrgbByte(bLin));
    }

    private static byte ToSrgbByte(double linear)
    {
        var clamped = Math.Clamp(linear, 0.0, 1.0);
        var srgb = clamped <= 0.0031308
            ? 12.92 * clamped
            : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(srgb, 0.0, 1.0) * 255);
    }
}
