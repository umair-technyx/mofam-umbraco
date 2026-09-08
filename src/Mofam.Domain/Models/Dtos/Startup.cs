namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// Everything the front end needs on first load: chrome (header/footer), translations
/// and the resolved culture — in one call rather than four.
/// </summary>
public sealed record Startup
{
    public string? Culture { get; init; }
    public required HeaderDto Header { get; init; }
    public required FooterDto Footer { get; init; }

    /// <summary>Dictionary key to translated value for the requested culture.</summary>
    public IReadOnlyDictionary<string, string> Dictionary { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Content type aliases the detail endpoint accepts —
    /// <c>GET /api/web/{culture}/{contentType}/{slug}</c> — so the front end knows which
    /// values are valid without hard-coding them on its side. Same list
    /// <c>ComponentMapper</c> uses to decide a picked page from a reusable component
    /// (<see cref="Mofam.Domain.Constants.CmsConstants.ContentTypes.PageTypes"/>): one
    /// source of truth, so a new page type can't go stale on one side of the API.
    /// </summary>
    public IReadOnlyList<string> ContentTypes { get; init; } = [];
}

public sealed record HeaderDto
{
    public MediaDto? PrimaryLogo { get; init; }
    public MediaDto? SecondaryLogo { get; init; }
    public IReadOnlyList<NavigationItemDto> Navigation { get; init; } = [];
}

public sealed record FooterDto
{
    public MediaDto? PrimaryLogo { get; init; }
    public MediaDto? SecondaryLogo { get; init; }
    public IReadOnlyList<NavigationItemDto> Navigation { get; init; } = [];
    public IReadOnlyList<LinkDto> SocialLinks { get; init; } = [];
    public string? FollowUsLabel { get; init; }
    public string? BottomText { get; init; }
}

public sealed record NavigationItemDto
{
    public required string Name { get; init; }
    public LinkDto? Link { get; init; }
    public IReadOnlyList<NavigationItemDto> Children { get; init; } = [];
}
