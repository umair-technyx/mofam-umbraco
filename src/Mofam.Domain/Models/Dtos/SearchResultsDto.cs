namespace Mofam.Domain.Models.Dtos;

public sealed record SearchResultsDto
{
    public required IReadOnlyList<PageDto> Items { get; init; }
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
