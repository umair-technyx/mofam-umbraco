using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
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

            return value switch
            {
                // A real page reached directly through this property (e.g. a content
                // picker, not a Block List/Grid) — shape it the same way any other
                // picked page is shaped, via the caller's resolver, not as a component.
                IEnumerable<IPublishedContent> multiPick => multiPick
                    .Select(c => ToComponentDto(resolvePage(c, ancestors)))
                    .ToList(),
                IPublishedContent singlePick => [ToComponentDto(resolvePage(singlePick, ancestors))],
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

    private ComponentDto MapPublishedContent(
        IPublishedElement content,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        HashSet<Guid> ancestors)
    {
        // A genuine content page reached from inside an authored element (e.g. a picker
        // property on a block) is not this mapper's shape to decide — hand it off exactly
        // like a top-level picked page, so a "grid item" component and a "gridItems" own-
        // field behave identically.
        if (content is IPublishedContent pageContent)
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
        HashSet<Guid> ancestors)
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
        HashSet<Guid> ancestors)
    {
        // Primitives, links, media and string lists are shared with every other endpoint.
        if (valueMapper.TryMapLeaf(value, culture, out var leaf))
        {
            return leaf;
        }

        // Everything from here recurses, either into a real page (resolved by the
        // caller — PageMapper — never flattened by this mapper) or into this mapper's
        // own ComponentDto shape for authored Block List/Grid elements.
        return value switch
        {
            BlockGridModel blockGrid => blockGrid.Select(i => MapPublishedContent(i.Content, culture, resolvePage, ancestors)).ToList(),
            BlockListModel blockList => blockList.Select(i => MapPublishedContent(i.Content, culture, resolvePage, ancestors)).ToList(),
            IEnumerable<IPublishedContent> pages => pages.Select(c => resolvePage(c, ancestors)).ToList(),
            IPublishedContent page => resolvePage(page, ancestors),
            _ => value?.ToString(),
        };
    }
}
