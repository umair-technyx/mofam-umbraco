namespace Mofam.Domain.Models.Dtos;

/// <summary>The answer of the global search: suggestions while typing, or one page of items on "See all".</summary>
public sealed record GlobalSearchDto
{
    /// <summary>Up to 3 titles of the best matches. Only while typing. Null when none.</summary>
    public IReadOnlyList<string>? SuggestedSearches { get; init; }

    // The fields below are filled only on "See all".

    /// <summary>One page of results. Services come first.</summary>
    public IReadOnlyList<SearchResultItemDto>? Items { get; init; }

    /// <summary>All results for the text. Null when none.</summary>
    public long? TotalResults { get; init; }

    /// <summary>The number of results in each category, for the filter panel.</summary>
    public IReadOnlyList<CategoryCountDto>? CategoryCounts { get; init; }

    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public int? TotalPages { get; init; }
    public bool? HasPreviousPage { get; init; }
    public bool? HasNextPage { get; init; }
}

public sealed record CategoryCountDto
{
    /// <summary>The key of the category. Send it back in filters.categories.</summary>
    public string? Key { get; init; }

    public string? Title { get; init; }
    public long Count { get; init; }
}

public sealed record SearchResultItemDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }

    /// <summary>The Page Category picked for the item (for example "Guidance"). Null when none.</summary>
    public string? PageCategory { get; init; }

    /// <summary>The first category, for example "Getting married". Null when none.</summary>
    public string? Category { get; init; }

    public string? Slug { get; init; }
    public LinkDto? Cta { get; init; }
}
