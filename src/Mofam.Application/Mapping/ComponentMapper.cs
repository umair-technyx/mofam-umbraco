using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Mofam.Application.Mapping;

public sealed class ComponentMapper(
    IVariationContextAccessor variationContextAccessor,
    IPropertyValueMapper valueMapper,
    IPublishedContentQuery contentQuery,
    ILogger logger) : IComponentMapper
{

    public IReadOnlyList<ComponentDto> MapComponents(
        IPublishedProperty? componentsProperty,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
    {
        if (componentsProperty is null) return [];

        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            if (!string.IsNullOrWhiteSpace(culture))
            {
                variationContextAccessor.VariationContext = new VariationContext(culture);
            }

            var value = componentsProperty.GetValue(culture) ?? componentsProperty.GetValue(null);

            // Every entry here is a real IPublishedContent — whether it's a genuine page
            // or a reusable component-library node is decided per item inside
            // MapPublishedContent, since a single picker (this property, or an own-field
            // picker like Service.Categories, both land here) can in principle point at
            // either kind.
            return value switch
            {
                IEnumerable<IPublishedContent> multiPick => multiPick
                    .Select(c => MapPublishedContent(c, culture, resolvePage, ancestors))
                    .ToList(),
                IPublishedContent singlePick => [MapPublishedContent(singlePick, culture, resolvePage, ancestors)],
                _ => [],
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MapComponents failed for property {Alias}, Culture={Culture}", componentsProperty.Alias, culture);
            return [];
        }
    }

    /// <summary>Wraps a resolved page reference in the component envelope so callers get one uniform list shape.</summary>
    private static ComponentDto ToComponentDto(PageDto page) => new()
    {
        Alias = page.ContentType,
        Properties = page,
    };

    /// <summary>
    /// A picker can point at two different kinds of content, and only the content type
    /// tells them apart: a genuine page (<see cref="CmsConstants.ContentTypes.PageTypes"/>
    /// — independently navigable, so it's shaped as a listing reference via
    /// <paramref name="resolvePage"/>) or a reusable component-library node (e.g.
    /// <c>startingPointsGrid</c>) meant to render in full wherever it's picked, exactly
    /// like an authored Block List element. Everything below this check is the
    /// flatten-in-full path, unchanged for either an element or a component-library node.
    /// </summary>
    private ComponentDto MapPublishedContent(
        IPublishedElement content,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
    {
        if (content is IPublishedContent pageContent
            && CmsConstants.ContentTypes.PageTypes.Contains(pageContent.ContentType.Alias))
        {
            return ToComponentDto(resolvePage(pageContent, ancestors));
        }

        // Guard against genuine cycles only — a fresh branch copy per recursive step, so
        // the same item legitimately appearing under two siblings is still mapped in full.
        if (ancestors.Contains(content.Key))
        {
            logger.Warning(
                "Cycle detected at {ContentType} ({Key}) — stopping recursion on this branch",
                content.ContentType.Alias, content.Key);

            return new ComponentDto
            {
                Alias = content.ContentType.Alias,
                Properties = new Dictionary<string, object?>
                {
                    ["circularReference"] = true,
                    ["key"] = content.Key,
                },
            };
        }

        var branch = new HashSet<Guid>(ancestors) { content.Key };

        return new ComponentDto
        {
            Alias = content.ContentType.Alias,
            Properties = MapProperties(content, culture, resolvePage, branch),
        };
    }

    private Dictionary<string, object?> MapProperties(
        IPublishedElement content,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
    {
        var result = new Dictionary<string, object?>();

        // Checked once per element, so every other element pays for one comparison only.
        var isThumbnailCards = string.Equals(content.ContentType.Alias, CmsConstants.ThumbnailCards.SectionAlias, StringComparison.OrdinalIgnoreCase);

        foreach (var property in content.Properties)
        {
            try
            {
                var value = property.GetValue(culture) ?? property.GetValue(null);

                result[property.Alias] =
                    isThumbnailCards
                    && value is IEnumerable<Link> links
                    && string.Equals(property.Alias, CmsConstants.ThumbnailCards.CardsAlias, StringComparison.OrdinalIgnoreCase)
                        ? BuildThumbnailCards(links, culture, resolvePage, ancestors)
                        : SanitizeValue(value, culture, resolvePage, ancestors);
            }
            catch (Exception ex)
            {
                logger.Warning(
                    ex,
                    "Skipping property {Alias} on {ContentType} — failed to map value",
                    property.Alias, content.ContentType.Alias);
                result[property.Alias] = null;
            }
        }
        return result;
    }

    // Builds the cards while the page is requested. Each link that picks a node (service, page...) becomes a card with
    // the title, description, shape and tags of that node, and the link as the CTA. A link to somewhere else
    // (manual or media) becomes a card with only the link, so nothing the editor added is lost.
    private List<ThumbnailCardDto>? BuildThumbnailCards(
        IEnumerable<Link> links,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
    {
        var cards = links.TryGetNonEnumeratedCount(out var count) ? new List<ThumbnailCardDto>(count) : [];

        foreach (var link in links)
        {
            // A link without an address is no use as a CTA, so it is sent as null.
            var cta = string.IsNullOrWhiteSpace(link.Url) ? null : LinkMapper.Map(link);

            var node = link.Type == LinkType.Content && link.Udi is GuidUdi udi ? contentQuery.Content(udi.Guid) : null;
            if (node is null)
            {
                // Nothing to show at all (no title, no link): the card is left out.
                if (Blank(link.Name) is null && cta is null) continue;

                cards.Add(new ThumbnailCardDto { Title = Blank(link.Name), Cta = cta });
                continue;
            }

            var tags = valueMapper.Raw(node, CmsConstants.ThumbnailCards.Tags, culture);
            var mappedTags = tags is null ? null : SanitizeValue(tags, culture, resolvePage, ancestors);

            // Each field comes only from the node's own field. An empty one is sent as null (no fallback).
            cards.Add(new ThumbnailCardDto
            {
                Title = Blank(valueMapper.Text(node, CmsConstants.ThumbnailCards.Title, culture)),
                Description = Blank(valueMapper.Text(node, CmsConstants.ThumbnailCards.Description, culture)),

                Shape = Blank(valueMapper.Text(node, CmsConstants.ThumbnailCards.BackgroundDecoration, culture)),

                // No tags (a page, or a service without any) is null, not an empty list.
                Tags = mappedTags switch
                {
                    null => null,
                    string text when string.IsNullOrWhiteSpace(text) => null,
                    System.Collections.ICollection { Count: 0 } => null,
                    _ => mappedTags,
                },
                Cta = cta,
            });
        }

        // No cards at all is null, not an empty list.
        return cards.Count == 0 ? null : cards;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private object? SanitizeValue(
        object? value,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
    {
        // Primitives, links, media and string lists are shared with every other endpoint.
        if (valueMapper.TryMapLeaf(value, culture, out var leaf))
        {
            return leaf;
        }

        // Everything from here recurses into MapPublishedContent, which decides per item
        // whether it's a page reference or a component-library node to flatten.
        return value switch
        {
            BlockGridModel blockGrid => blockGrid.Select(i => MapPublishedContent(i.Content, culture, resolvePage, ancestors)).ToList(),
            BlockListModel blockList => blockList.Select(i => MapPublishedContent(i.Content, culture, resolvePage, ancestors)).ToList(),
            IEnumerable<IPublishedContent> pages => pages.Select(c => MapPublishedContent(c, culture, resolvePage, ancestors)).ToList(),
            IPublishedContent page => MapPublishedContent(page, culture, resolvePage, ancestors),
            _ => value?.ToString(),
        };
    }
}
