namespace Mofam.Domain.Constants;

/// <summary>
/// Search and filter wiring. These are tied to the content model, not the environment,
/// so they live in code rather than appsettings — nothing here differs between dev,
/// staging and production.
/// </summary>
public static class SearchConstants
{
    /// <summary>
    /// Umbraco's built-in index of published content only. Never contains drafts, which
    /// is what makes it safe to expose publicly.
    /// </summary>
    public const string IndexName = "ExternalIndex";

    /// <summary>
    /// Index fields a free-text term is matched against.
    /// <para>
    /// List the bare alias only. Umbraco indexes culture-variant properties with a
    /// culture suffix (<c>title_en</c>) and invariant ones without, so the query expands
    /// each name into both forms at query time.
    /// </para>
    /// </summary>
    public static readonly string[] SearchableFields = ["title", "description"];

    /// <summary>
    /// Content types callers may search. Anything not listed is unreachable through the
    /// API, so adding a type here is a deliberate act.
    /// </summary>
    public static readonly string[] AllowedContentTypes =
    [
        CmsConstants.ContentTypes.Service,
    ];

    /// <summary>
    /// Filters exposed per content type. Options are every published node of
    /// <see cref="FilterSource.ItemContentTypeAlias"/> in the CMS — not only the ones
    /// currently assigned — so an editor can add a category and have it appear as a
    /// filter before anything uses it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FilterSource[]> FilterDefinitions =
        new Dictionary<string, FilterSource[]>(StringComparer.OrdinalIgnoreCase)
        {
            [CmsConstants.ContentTypes.Service] =
            [
                new FilterSource(CmsConstants.Fields.Categories, CmsConstants.ContentTypes.ServiceCategory),
            ],
        };

    /// <summary>Filter options only change on publish, so they cache comfortably.</summary>
    public static readonly TimeSpan FilterCacheDuration = TimeSpan.FromMinutes(5);
}

/// <summary>
/// One filter on a listing.
/// </summary>
/// <param name="Key">
/// Property alias the client sends back to the search endpoint. This has to match the
/// property being filtered on, not the container's name, or the round-trip won't match.
/// </param>
/// <param name="ItemContentTypeAlias">Content type supplying the available options.</param>
public sealed record FilterSource(string Key, string ItemContentTypeAlias);
