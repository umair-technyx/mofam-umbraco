namespace Mofam.Domain.Models.Requests;

public sealed record SearchRequest
{
    /// <summary>Free-text term. When empty the query returns everything matching the filters.</summary>
    public string? Query { get; init; }

    /// <summary>
    /// Content type to list, e.g. <c>service</c>. Validated against
    /// <c>SearchConstants.AllowedContentTypes</c>, so a caller cannot reach content the
    /// API was never meant to expose. Omit to search everything allowed.
    /// </summary>
    public string? ContentType { get; init; }

    public string? Culture { get; init; }

    /// <summary>
    /// Keys come from the filters endpoint: the group key is the property alias, the
    /// values are the option keys the user selected.
    /// </summary>
    public Dictionary<string, string[]>? Filters { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
