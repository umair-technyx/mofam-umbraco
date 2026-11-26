using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Mofam.Domain.Constants;
using Umbraco.Cms.Core.Strings;

namespace Mofam.Application.Helpers;
public static partial class CommonHelper
{
    #region Email

    [GeneratedRegex(GeneralConstants.EmailPattern, RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailRegex();
    public static bool IsValidEmail(string? value) => !IsBlank(value) && new EmailAddressAttribute().IsValid(value) && Matches(EmailRegex(), value!);
    private static bool Matches(Regex pattern, string value)
    {
        try
        {
            return pattern.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    #endregion

    #region Text

    public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
    public static bool HasLineBreak(string? value) => value is not null && value.Any(c => char.IsControl(c) || c is (char)0x2028 or (char)0x2029);

    private const char LineFeed = (char)10;
    private const char CarriageReturn = (char)13;

    // One sanitizer for each thread, because a sanitizer is not safe to share. Nothing is allowed, so every tag is removed.
    private static readonly ThreadLocal<HtmlSanitizer> PlainTextSanitizer = new(() =>
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.KeepChildNodes = true; // the text inside a removed tag stays
        return sanitizer;
    });

    // Removes all HTML tags and scripts and returns the plain text. Line breaks are made the same (one line feed).
    public static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // The sanitizer gives HTML text (for example &amp;), so it is turned back into plain text.
        var text = WebUtility.HtmlDecode(PlainTextSanitizer.Value!.Sanitize(value));

        return text.Replace(CarriageReturn.ToString() + LineFeed, LineFeed.ToString()).Replace(CarriageReturn, LineFeed);
    }

    #endregion

    #region Slug

    // One slug rule for saving and for the API lookup, so they always match. Returns null when nothing is left.
    public static string? NormaliseSlug(string? value, IShortStringHelper shortStringHelper)
    {
        // "/About Us/" and "about-us" must become the same.
        var trimmed = value?.Trim().Trim('/').Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        var cleaned = shortStringHelper.CleanStringForUrlSegment(trimmed);

        // Umbraco returns empty for Arabic, so the editor's own value is kept.
        return string.IsNullOrWhiteSpace(cleaned) ? trimmed.ToLowerInvariant() : cleaned;
    }

    #endregion

    #region Examine culture

    // Examine uses "alias_culture" for variant properties and "alias" for invariant ones, so both are queried.
    public static string[] ExpandForCulture(string[] aliases, string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return aliases;

        var expanded = new List<string>(aliases.Length * 2);

        foreach (var alias in aliases)
        {
            expanded.Add(alias);
            expanded.Add($"{alias}_{culture}");
        }

        return [.. expanded];
    }

    #endregion

    #region Csv

    // One CSV writer for all the exports. headers: the column names. rows: the text of each column of each row.
    // phoneColumn: the position of a phone column (or -1 when there is none).
    public static byte[] BuildCsv(string[] headers, IEnumerable<string?[]> rows, int phoneColumn = -1)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(',', headers.Select(CsvField)));

        foreach (var row in rows)
        {
            text.AppendLine(string.Join(',', row.Select((cell, column) => column == phoneColumn ? CsvPhone(cell) : CsvField(cell))));
        }

        return CsvBytes(text.ToString());
    }

    // Every field is in quotes. A text that starts with = + - @ is made plain text with a ' in front,
    // or Excel would run it as a formula (a visitor could type one in the form).
    private static string CsvField(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r') text = "'" + text;

        return '"' + text.Replace("\"", "\"\"") + '"';
    }

    // A phone number (+971500000000) is written as ="+971500000000", so Excel keeps the + and all the digits as text.
    // Only a text of digits, spaces, brackets, - and a + at the start gets this, so it cannot hold a formula.
    private static string CsvPhone(string? value)
    {
        var text = value?.Trim() ?? string.Empty;

        return CsvPhonePattern().IsMatch(text) ? "\"=\"\"" + text + "\"\"\"" : CsvField(text);
    }

    [GeneratedRegex(@"^\+?[0-9 ()\-]{3,50}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CsvPhonePattern();

    // UTF-8 with a mark at the start, so Excel reads Arabic text correctly.
    private static byte[] CsvBytes(string text) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];

    #endregion
}
