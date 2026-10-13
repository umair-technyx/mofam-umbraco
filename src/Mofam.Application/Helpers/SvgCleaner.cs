using System.Text.RegularExpressions;

namespace Mofam.Application.Helpers;

// Cleans SVG code that an editor pasted into a text field, so the API sends it as one tidy line.
// Text that is not SVG is returned as it is.
public static class SvgCleaner
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex SpaceBetweenTags = new(@">\s+<", RegexOptions.Compiled, RegexTimeout);

    // name="value" -> name='value', only when the value has no quote inside.
    // In JSON this removes the \" escapes, so the SVG is easy to read in the response.
    private static readonly Regex DoubleQuotedAttribute =
        new(@"(\s[\w:.-]+)=""([^""']*)""", RegexOptions.Compiled, RegexTimeout);

    public static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var svg = text.Trim();

        // Sometimes the SVG is saved with the characters \" and \n written out as text.
        // Turn them back into normal quotes and spaces.
        if (svg.Contains("\\\"") || svg.StartsWith("\\n", StringComparison.Ordinal))
        {
            svg = svg
                .Replace("\\r", " ")
                .Replace("\\n", " ")
                .Replace("\\t", " ")
                .Replace("\\\"", "\"")
                .Trim();
        }

        if (!svg.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)) return text;

        svg = Whitespace.Replace(svg, " ");
        svg = SpaceBetweenTags.Replace(svg, "><");
        svg = DoubleQuotedAttribute.Replace(svg, "$1='$2'");

        return svg;
    }
}
