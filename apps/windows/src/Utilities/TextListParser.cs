// Converts free-text form fields to/from lists, e.g. "rapide, vif" becomes
// ["rapide", "vif"].
namespace LexiCall.Desktop.Utilities;

public static class TextListParser
{
    public static List<string> ParseCommaSeparatedText(string value)
    {
        return value
            .Split([',', ';', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToList();
    }

    public static List<string> ParseLineSeparatedText(string value)
    {
        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToList();
    }

    public static string FormatCommaSeparatedText(IEnumerable<string> values)
    {
        return string.Join(", ", values);
    }

    public static string FormatLineSeparatedText(IEnumerable<string> values)
    {
        return string.Join(Environment.NewLine, values);
    }
}
