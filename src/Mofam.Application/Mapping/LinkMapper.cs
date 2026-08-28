using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Mapping;

/// <summary>
/// Link -> LinkDto. Pure input/output with no services, so it is safe as a static class.
/// </summary>
public static class LinkMapper
{
    public static LinkDto? Map(Link? link)
    {
        if (link is null) return null;

        return new LinkDto
        {
            Url = link.Url,
            Name = link.Name,
            Target = link.Target,
            //Type = link.Type.ToString(),
        };
    }

    public static IReadOnlyList<LinkDto> MapMany(IEnumerable<Link>? links)
    {
        if (links is null) return [];

        return links
            .Select(Map)
            .Where(l => l is not null)
            .Select(l => l!)
            .ToList();
    }

    /// <summary>
    /// Returns the first link held by any property on the element, whichever alias it
    /// uses — each navigation item type names its link property differently
    /// (headerPrimaryNavigationItem, footerSecondaryNavigationItem, and so on).
    /// </summary>
    public static LinkDto? FirstLinkOn(IPublishedElement element, string? culture)
    {
        foreach (var property in element.Properties)
        {
            var value = property.GetValue(culture) ?? property.GetValue(null);

            switch (value)
            {
                case Link single:
                    return Map(single);
                case IEnumerable<Link> many when many.Any():
                    return Map(many.First());
            }
        }

        return null;
    }
}
