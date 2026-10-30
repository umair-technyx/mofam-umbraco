namespace Mofam.Domain.Models.Dtos;

#region Startup

// Everything the front end needs on first load: header, footer, translations
// and culture, in one call instead of four.
public sealed record Startup
{
    public string? Culture { get; init; }
    public required HeaderDto Header { get; init; }
    public required FooterDto Footer { get; init; }
    public required ScriptsDto Scripts { get; init; }

    // Dictionary key and its translated value for the requested culture.
    public IReadOnlyDictionary<string, string> Dictionary { get; init; } = new Dictionary<string, string>();

    // Content type aliases the detail endpoint accepts:
    // GET /api/web/{culture}/{contentType}/{slug}
    // The front end can use this list instead of hard-coding the values.
    // It is the same list ComponentMapper uses (CmsConstants.ContentTypes.PageTypes),
    // so a new page type is added in one place only.
    public IReadOnlyList<string> ContentTypes { get; init; } = [];
}

#endregion

#region Header and Footer

public sealed record HeaderDto
{
    public string? TopBarLabel { get; init; }
    public LinkDto? LearnMoreCta { get; init; }
    public MediaDto? PrimaryLogo { get; init; }
    public MediaDto? SecondaryLogo { get; init; }
    public string? LogoAltText { get; init; }
    public IReadOnlyList<NavigationItemDto> Navigation { get; init; } = [];
    public string? EmailLabel { get; init; }
    public string? EmailInputText { get; init; }
    public string? PhoneLabel { get; init; }
    public string? PhoneInputText { get; init; }
    public string? SocialIconsLabel { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> SocialIcons { get; init; } = [];
}

public sealed record FooterDto
{
    public MediaDto? PrimaryLogo { get; init; }
    public MediaDto? SecondaryLogo { get; init; }
    public string? LogoAltText { get; init; }
    public IReadOnlyList<NavigationItemDto> Navigation { get; init; } = [];
    public IReadOnlyList<LinkDto> SocialLinks { get; init; } = [];
    public string? FollowUsLabel { get; init; }
    public string? BottomText { get; init; }
}

#endregion

#region Scripts

public sealed record ScriptsDto
{
    public string? AccessibilityScript { get; init; }
}
#endregion

#region Navigation

public sealed record NavigationItemDto
{
    public required string Name { get; init; }
    public LinkDto? Link { get; init; }
    public IReadOnlyList<NavigationItemDto> Children { get; init; } = [];
}

#endregion
