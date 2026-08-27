using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

/// <summary>
/// Turns Umbraco media nodes into front-end-ready <see cref="MediaDto"/> values.
/// Without this, media reaches the client as an unusable property bag with no URL.
/// </summary>
public interface IMediaUrlBuilder
{
    /// <summary>Returns null when the item is missing or is not a media node.</summary>
    MediaDto? Build(IPublishedContent? media, string? culture = null);

    IReadOnlyList<MediaDto> BuildMany(IEnumerable<IPublishedContent>? media, string? culture = null);

    /// <summary>True when the value is a media node this builder should handle.</summary>
    bool IsMedia(object? value);
}
