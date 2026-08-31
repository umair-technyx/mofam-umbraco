namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// Filter options for a content type, built from published content so an option with
/// no matching items never appears. The client sends the selected keys back to the
/// search endpoint.
/// </summary>
public sealed record FilterDataDto
{
    public required string ContentType { get; init; }
    public IReadOnlyList<FilterGroupDto> Filters { get; init; } = [];
}

public sealed record FilterGroupDto
{
    /// <summary>Property alias — the key the client sends back when filtering.</summary>
    public required string Key { get; init; }

    public IReadOnlyList<FilterValueDto> Values { get; init; } = [];
}

public sealed record FilterValueDto
{
    /// <summary>Value the client sends back for this option.</summary>
    public required string Key { get; init; }

    /// <summary>Display text.</summary>
    public required string Value { get; init; }
}
