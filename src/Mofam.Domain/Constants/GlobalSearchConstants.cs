namespace Mofam.Domain.Constants;

/// <summary>Constants of the global search. The listing search has its own: <see cref="SearchConstants"/>.</summary>
public static class GlobalSearchConstants
{
    // The types the search can find.
    public static readonly string[] AllowedContentTypes =
    [
        CmsConstants.ContentTypes.Service,
        CmsConstants.ContentTypes.Resource,
        CmsConstants.ContentTypes.Media,
        CmsConstants.ContentTypes.News,
        CmsConstants.ContentTypes.Story,
        CmsConstants.ContentTypes.Leader,
        CmsConstants.ContentTypes.Page,
    ];

    // The "Has detail page" toggle of Card Settings. Only items with it on are found.
    public const string HasDetailPageAlias = "hasDetailPage";

    // The Page Tag field of the items (for example "Guidance"). The tags are the Page Tags data source.
    public const string PageTagAlias = "pageTag";

    // The fields the text is matched against.
    public static readonly string[] SearchableFields = [CmsConstants.Fields.Title, CmsConstants.Fields.Description];

    // The page size of "See all" when none is sent.
    public const int DefaultPageSize = 10;

    // The most values used for one filter. More would make the query too big for Lucene.
    public const int MaxFilterValues = 50;

    // Words the index does not keep. They are skipped, or "people of determination" would find nothing.
    public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "if", "in", "into", "is", "it",
        "no", "not", "of", "on", "or", "such", "that", "the", "their", "then", "there", "these", "they", "this", "to", "was", "will", "with",
    };

    // The sort options of "See all". Relevance (best match first) is the default.
    public const string SortRelevance = "relevance";
    public const string SortNewest = "newest";

    // The index field with the last publish date.
    public const string UpdateDateField = "updateDate";

    // The least characters to search, and how many suggestions to show.
    public const int MinQueryLength = 3;
    public const int SuggestedSearchCount = 3;
}
