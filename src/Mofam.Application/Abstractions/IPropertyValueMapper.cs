using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

/// <summary>
/// Converts individual Umbraco property values into front-end shapes.
/// <para>
/// This is the single place that decides how a link, an image or a primitive becomes
/// JSON. Callers keep their own response shape and reuse this for the leaves, so the
/// rules cannot drift apart between endpoints.
/// </para>
/// </summary>
public interface IPropertyValueMapper
{
    /// <summary>
    /// Maps values this mapper owns outright — primitives, links, media, string lists.
    /// Returns false for values needing caller-specific recursion (blocks, content nodes),
    /// leaving the caller to decide how those are shaped.
    /// </summary>
    bool TryMapLeaf(object? value, string? culture, out object? mapped);

    /// <summary>Raw property value with an invariant fallback.</summary>
    object? Raw(IPublishedElement element, string alias, string? culture);

    string? Text(IPublishedElement element, string alias, string? culture);

    MediaDto? Media(IPublishedElement element, string alias, string? culture);

    IReadOnlyList<LinkDto> Links(IPublishedElement element, string alias, string? culture);
}
