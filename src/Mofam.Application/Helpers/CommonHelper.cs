using Umbraco.Cms.Core.Strings;

namespace Mofam.Application.Helpers;

/// <summary>
/// Pure, stateless helpers shared across the application layer.
/// <para>
/// Anything here must be a genuine cross-cutting utility with no state and no I/O.
/// Logic that belongs to one feature belongs with that feature, not in this class —
/// keep dependencies as parameters so callers stay in control of what they inject.
/// </para>
/// </summary>
public static class CommonHelper
{
    /// <summary>
    /// The single definition of what a slug looks like.
    /// <para>
    /// Both ends of the slug contract have to agree: the save-time handler that writes
    /// the stored value, and the API that matches an incoming request against it. When
    /// the two drift apart, content that saves cleanly stops resolving — the stored slug
    /// and the requested slug get normalised by different rules and never compare equal.
    /// </para>
    /// <para>
    /// Takes <see cref="IShortStringHelper"/> as a parameter rather than being wrapped in
    /// an injected service, matching how Umbraco shapes its own equivalents
    /// (<c>StringExtensions.ToUrlSegment</c>, <c>ToSafeAlias</c>).
    /// </para>
    /// </summary>
    /// <returns>
    /// The canonical form of <paramref name="value"/>, or null when nothing usable is
    /// left after cleaning.
    /// </returns>
    public static string? NormaliseSlug(string? value, IShortStringHelper shortStringHelper)
    {
        // Editors paste slugs with stray slashes and whitespace, so "/About Us/" and
        // "about-us" have to collapse to the same thing on both sides.
        var trimmed = value?.Trim().Trim('/').Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        var cleaned = shortStringHelper.CleanStringForUrlSegment(trimmed);

        // CleanStringForUrlSegment returns empty for scripts it does not transliterate,
        // Arabic among them; keep the editor's own value rather than wiping it.
        return string.IsNullOrWhiteSpace(cleaned) ? trimmed.ToLowerInvariant() : cleaned;
    }
}
