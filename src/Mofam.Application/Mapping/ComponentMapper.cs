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
    IMediaUrlBuilder mediaUrlBuilder,
    ILogger logger) : IComponentMapper
{
    public IReadOnlyList<ComponentDto> MapComponents(IPublishedProperty? componentsProperty, string? culture)
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
            var ancestors = new HashSet<Guid>();

            return value switch
            {
                IEnumerable<IPublishedContent> multiPick => multiPick.Select(c => MapPublishedContent(c, culture, ancestors)).ToList(),
                IPublishedContent singlePick => [MapPublishedContent(singlePick, culture, ancestors)],
                _ => [],
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MapComponents failed for property {Alias}, Culture={Culture}", componentsProperty.Alias, culture);
            return [];
        }
    }

    private ComponentDto MapPublishedContent(IPublishedElement content, string? culture, HashSet<Guid> ancestors)
    {
        // Guard against genuine cycles only — a fresh set per branch, so the same item
        // legitimately appearing under two siblings is still mapped in full.
        if (ancestors.Contains(content.Key))
        {
            logger.Warning(
                "Cycle detected at {ContentType} ({Key}) — stopping recursion on this branch",
                content.ContentType.Alias, content.Key);

            return new ComponentDto
            {
                Alias = content.ContentType.Alias,
                Properties = new Dictionary<string, object?>(),
            };
        }

        var branch = new HashSet<Guid>(ancestors) { content.Key };

        return new ComponentDto
        {
            Alias = content.ContentType.Alias,
            Properties = MapProperties(content, culture, branch),
        };
    }

    private Dictionary<string, object?> MapProperties(IPublishedElement content, string? culture, HashSet<Guid> ancestors)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in content.Properties)
        {
            try
            {
                var value = property.GetValue(culture) ?? property.GetValue(null);
                result[property.Alias] = SanitizeValue(value, culture, ancestors);
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

    private object? SanitizeValue(object? value, string? culture, HashSet<Guid> ancestors)
    {
        switch (value)
        {
            case null:
                return null;

            case string or bool or int or long or double or decimal or Guid or DateTime or DateTimeOffset:
                return value;

            // Media must be resolved to a URL before the content-node branches below,
            // otherwise it serialises as a property bag with no usable link.
            case IPublishedContent media when media.ItemType == PublishedItemType.Media:
                return mediaUrlBuilder.Build(media, culture);

            case IEnumerable<IPublishedContent> mediaList when mediaUrlBuilder.IsMedia(mediaList):
                return mediaUrlBuilder.BuildMany(mediaList, culture);

            case BlockGridModel blockGrid:
                return blockGrid.Select(i => MapPublishedContent(i.Content, culture, ancestors)).ToList();

            case BlockListModel blockList:
                return blockList.Select(i => MapPublishedContent(i.Content, culture, ancestors)).ToList();

            case IEnumerable<IPublishedContent> list:
                return list.Select(c => MapPublishedContent(c, culture, ancestors)).ToList();

            case IPublishedContent content:
                return MapPublishedContent(content, culture, ancestors);

            case IEnumerable<string> strings:
                return strings.ToList();

            default:
                return value.ToString();
        }
    }
}
