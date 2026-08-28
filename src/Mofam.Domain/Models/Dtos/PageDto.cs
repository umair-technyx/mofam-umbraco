namespace Mofam.Domain.Models.Dtos;

public sealed record PageDto
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }

    /// <summary>From the metaTags composition — present on every type composed with it.</summary>
    public SeoDto Seo { get; init; } = new();

    public IReadOnlyList<ComponentDto> Components { get; init; } = [];
}

public sealed record ComponentDto
{
    public required string Alias { get; init; }
    public required object Properties { get; init; }
}
