namespace Mofam.Domain.Models.Dtos;

#region Filter data

// Filter options for a content type, built from published content,
// so an option with no matching items never appears.
// The client sends the selected keys back to the search endpoint.
public sealed record FilterDataDto
{
    public required string ContentType { get; init; }
    public IReadOnlyList<FilterGroupDto> Filters { get; init; } = [];
}

#endregion

#region Filter group

public sealed record FilterGroupDto
{
    // Property alias. The client sends this key back when filtering.
    public required string Key { get; init; }

    public IReadOnlyList<FilterValueDto> Values { get; init; } = [];
}

#endregion

#region Filter value

public sealed record FilterValueDto
{
    // Value the client sends back for this option.
    public required string Key { get; init; }

    // Text to show to the user.
    public required string Value { get; init; }
}

#endregion
