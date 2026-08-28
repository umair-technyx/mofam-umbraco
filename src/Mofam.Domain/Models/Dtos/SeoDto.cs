namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// Everything from the metaTags composition, grouped the way it is consumed —
/// each section maps to one family of tags in the document head.
/// </summary>
public sealed record SeoDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
    public string? CanonicalUrl { get; init; }

    /// <summary>Raw JSON-LD to emit in a script tag, when the editor supplied any.</summary>
    public string? SchemaJson { get; init; }

    public RobotsDto Robots { get; init; } = new();
    public OpenGraphDto OpenGraph { get; init; } = new();
    public TwitterCardDto Twitter { get; init; } = new();

    /// <summary>Alternate language URLs, ready to emit as link rel="alternate".</summary>
    public IReadOnlyList<HreflangDto> Hreflang { get; init; } = [];

    public SitemapDto Sitemap { get; init; } = new();
}

public sealed record RobotsDto
{
    public bool Index { get; init; }
    public bool Follow { get; init; }
}

public sealed record OpenGraphDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public MediaDto? Image { get; init; }

    /// <summary>Set when the editor supplied an external image URL instead of media.</summary>
    public string? ExternalImageUrl { get; init; }
}

public sealed record TwitterCardDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public MediaDto? Image { get; init; }
}

public sealed record HreflangDto
{
    /// <summary>Language code, or <c>x-default</c> for the fallback entry.</summary>
    public required string Hreflang { get; init; }
    public required string Href { get; init; }
}

public sealed record SitemapDto
{
    public bool Hide { get; init; }
    public int? Priority { get; init; }
    public string? ChangeFrequency { get; init; }
    public DateTime? LastUpdated { get; init; }
}
