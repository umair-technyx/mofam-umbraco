using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

/// <summary>
/// Builds the SEO block from the metaTags composition. Every content type composed
/// with metaTags maps identically, so page, service and serviceCategory all reuse this.
/// </summary>
public interface ISeoMapper
{
    SeoDto Map(IPublishedElement content, string? culture);
}
