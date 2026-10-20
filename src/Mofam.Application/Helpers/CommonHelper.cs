using System.ComponentModel.DataAnnotations;
using System.Net;
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
}
