using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Serilog;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Mofam.Application.Mapping;

public sealed class ComponentMapper(
    IVariationContextAccessor variationContextAccessor,
    IPropertyValueMapper valueMapper,
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
        foreach (var property in content.Properties)
        {
            try
            {
                var value = property.GetValue(culture) ?? property.GetValue(null);
                result[property.Alias] = SanitizeValue(value, culture, resolvePage, ancestors);
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
