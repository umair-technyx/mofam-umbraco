namespace Mofam.Domain.Models.Dtos;

public sealed record SearchHitDto
{
    public required string Id { get; init; }
    public required string ContentType { get; init; }
    public required string Name { get; init; }
    public string? Slug { get; init; }
    public string? Summary { get; init; }
    public MediaDto? Image { get; init; }

    /// <summary>Lucene relevance score for this hit, useful for debugging ranking.</summary>
    public float Score { get; init; }
}

public sealed record SearchResultsDto
{
    public required IReadOnlyList<SearchHitDto> Items { get; init; }
    public required long TotalResults { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static SearchResultsDto Empty(int pageNumber, int pageSize) => new()
    {
        Items = [],
        TotalResults = 0,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalPages = 0,
    };
}
