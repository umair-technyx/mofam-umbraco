using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Mapping;

// Converts a Umbraco Link to a LinkDto. It uses no services, so it can be a static class.
public static class LinkMapper
{
    #region Map

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

    #endregion

    #region Find link

    // Returns the first link found on any property of the element.
    // Each navigation item type has a different link property name,
    // so we look at the value type instead of the name.
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

    #endregion
}
