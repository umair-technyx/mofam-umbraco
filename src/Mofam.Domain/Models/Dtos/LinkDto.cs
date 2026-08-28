namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// A link from a URL picker, flattened for the front end. Without this, Umbraco's
/// <c>Link</c> type serialises as its type name rather than anything usable.
/// </summary>
public sealed record LinkDto
{
    public string? Url { get; init; }
    public string? Name { get; init; }

    /// <summary>e.g. <c>_blank</c>; null when the link opens in the same tab.</summary>
    public string? Target { get; init; }

    /// <summary>Content, Media or External.</summary>
    //public string? Type { get; init; }
}
