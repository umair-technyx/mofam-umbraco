using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

public enum PageMapMode
{
    /// <summary>Everything — the item's fields, its detail components and SEO.</summary>
    Detail = 0,

    /// <summary>
    /// Listing/search entry: the same model minus <c>detailPageComponents</c> and SEO,
    /// neither of which a card renders.
    /// </summary>
    Listing = 1,
}

/// <summary>
/// Single source of truth for turning content into a <see cref="PageDto"/>. Detail and
/// search both go through here so the two responses cannot drift apart.
/// </summary>
public interface IPageMapper
{
    PageDto Map(IPublishedContent content, string? culture, PageMapMode mode);
}
