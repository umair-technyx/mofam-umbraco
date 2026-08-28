namespace Mofam.Domain.Models.Requests;

public enum SearchSortBy
{
    Relevance = 0,
    NewestFirst = 1,
    OldestFirst = 2,
    NameAscending = 3,
}

public sealed record SearchRequest
{
    /// <summary>Free-text term. When empty the query returns everything matching the filters.</summary>
    public string? Query { get; init; }

    /// <summary>
    /// Content type aliases to search. Validated against <c>Search:AllowedContentTypes</c>,
    /// so a caller cannot reach content the API was never meant to expose.
    /// </summary>
    public string[]? ContentTypes { get; init; }

    public string? Culture { get; init; }

    /// <summary>Exact-match field filters, e.g. <c>topic = innovation</c>.</summary>
    public Dictionary<string, string[]>? Filters { get; init; }

    public SearchSortBy SortBy { get; init; } = SearchSortBy.Relevance;

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
