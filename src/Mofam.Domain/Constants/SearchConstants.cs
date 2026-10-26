namespace Mofam.Domain.Constants;
public static class SearchConstants
{
    #region Index

    // Umbraco's built-in index that has only published content. It never has drafts,
    // so it is safe to expose publicly.
    public const string IndexName = "ExternalIndex";

    // Examine's built-in field with the content type alias of a node. It is set on every
    // indexed document, so any lookup can filter on it directly.
    public const string NodeTypeAliasField = "__NodeTypeAlias";

    // Examine's built-in published flag. Content with cultures is flagged per culture
    // (__Published_en). Content without cultures uses the field as it is.
    // Callers add the culture suffix themselves when needed.
    public const string PublishedField = "__Published";

    #endregion

    #region Search

    // Index fields that a free-text term is matched against.
    // List only the plain alias. Umbraco indexes culture properties with a suffix
    // (title_en) and others without, so the query adds both forms at query time.
    public static readonly string[] SearchableFields = ["title", "description"];

    // Content types that callers can search. Anything not listed cannot be reached
    // through the API, so adding a type here is a deliberate choice.
    public static readonly string[] AllowedContentTypes =
    [
        CmsConstants.ContentTypes.Service,
        CmsConstants.ContentTypes.Resource,
    ];

    // Allowed page size for a search. A value outside this range is moved into it.
    public const int MinPageSize = 1;
    public const int MaxPageSize = 50;

    #endregion

    #region Filters

    // Filters shown for each content type. The options are every published node of
    // FilterSource.ItemContentTypeAlias in the CMS, not only the ones already in use.
    // So an editor can add a category and it shows as a filter before anything uses it.
    public static readonly IReadOnlyDictionary<string, FilterSource[]> FilterDefinitions =
        new Dictionary<string, FilterSource[]>(StringComparer.OrdinalIgnoreCase)
        {
            [CmsConstants.ContentTypes.Service] =
            [
                new FilterSource(CmsConstants.Fields.Categories, CmsConstants.ContentTypes.ServiceCategory),
            ],
        };

    // Filter options only change on publish, so they can be cached.
    public static readonly TimeSpan FilterCacheDuration = TimeSpan.FromMinutes(5);

    #endregion
}

#region Filter source

// One filter on a listing.
// Key: property alias the client sends back to the search endpoint. It must match the
// property being filtered on, not the container name, or the values will not match.
// ItemContentTypeAlias: content type that supplies the available options.
public sealed record FilterSource(string Key, string ItemContentTypeAlias);

#endregion
