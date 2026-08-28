namespace Mofam.Domain.Options;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>Examine index to query. Defaults to Umbraco's built-in ExternalIndex.</summary>
    public string IndexName { get; set; } = "ExternalIndex";

    /// <summary>
    /// Index fields a free-text term is matched against. These must be fields Examine
    /// actually indexes, otherwise the term silently matches nothing.
    /// </summary>
    public string[] SearchableFields { get; set; } = ["nodeName", "title", "description"];

    /// <summary>
    /// Content types callers are allowed to search. Empty means "allow any", which is
    /// fine in development but should be filled in before going live.
    /// </summary>
    public string[] AllowedContentTypes { get; set; } = [];

    /// <summary>Upper bound on page size, so a caller cannot request the whole index.</summary>
    public int MaxPageSize { get; set; } = 50;

    /// <summary>
    /// Property aliases checked, in order, for a thumbnail to attach to each hit.
    /// The first one that resolves to media wins.
    /// </summary>
    public string[] ImageFieldAliases { get; set; } = ["thumbnailImageWeb", "thumbnailImage", "image"];
}
