using System.Text.Json.Serialization;

namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// One content item. The same model serves both a detail page and a listing entry —
/// a listing simply omits what it cannot render: <c>detailPageComponents</c> and SEO.
/// </summary>
public sealed record PageDto
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }

    /// <summary>Content type alias, so a client can branch on what it received.</summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Detail-page components. Empty on listing responses.
    /// </summary>
    public IReadOnlyList<ComponentDto> Components { get; init; } = [];

    /// <summary>
    /// From the metaTags composition. Null on listing responses — SEO belongs to the
    /// page being rendered, not to a card in a list.
    /// </summary>
    public SeoDto? Seo { get; init; }

    /// <summary>
    /// The item's own fields — description, categories, images and so on.
    /// <para>
    /// Serialised INLINE at the root of the object rather than under a wrapper, so a
    /// service's <c>description</c> sits alongside <c>title</c>. Only genuinely distinct
    /// concerns (components, seo) stay grouped.
    /// </para>
    /// Must stay a mutable <c>Dictionary&lt;string, object?&gt;</c>: System.Text.Json
    /// rejects read-only dictionaries for extension data.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?> Properties { get; init; } = [];
}

public sealed record ComponentDto
{
    public required string Alias { get; init; }
    public required object Properties { get; init; }
}
