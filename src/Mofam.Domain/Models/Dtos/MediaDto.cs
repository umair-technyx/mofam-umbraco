namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// A media item shaped for the front end: a usable URL plus the metadata a client
/// needs to lay it out without a second request.
/// </summary>
public sealed record MediaDto
{
    public required string Url { get; init; }
    public string? Name { get; init; }
    public string? AltText { get; init; }
    public string? Extension { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public long? Bytes { get; init; }
}
