using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

// Converts single Umbraco property values into the shape the front end needs.
// This is the one place that decides how a link, image or simple value becomes JSON.
// Callers keep their own response shape and reuse this for the small parts,
// so the rules stay the same in every endpoint.
public interface IPropertyValueMapper
{
    #region Generic mapping

    // Maps the values this mapper handles fully: simple values, links, media, string lists.
    // Returns false for values that need caller-specific handling (blocks, content nodes),
    // so the caller decides how to shape those.
    bool TryMapLeaf(object? value, string? culture, out object? mapped);

    // Raw property value. Falls back to the invariant value if the culture has none.
    object? Raw(IPublishedElement element, string alias, string? culture);

    #endregion

    #region Typed values

    string? Text(IPublishedElement element, string alias, string? culture);

    MediaDto? Media(IPublishedElement element, string alias, string? culture);

    IReadOnlyList<LinkDto> Links(IPublishedElement element, string alias, string? culture);

    #endregion
}
