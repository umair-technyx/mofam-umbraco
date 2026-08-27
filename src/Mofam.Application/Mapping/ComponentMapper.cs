using Serilog;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Mapping;

public sealed class ComponentMapper(IVariationContextAccessor variationContextAccessor, ILogger logger) : IComponentMapper
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
            var visitedKeys = new HashSet<Guid>();

            return value switch
            {
                IEnumerable<IPublishedContent> multiPick => multiPick.Select(c => MapPublishedContent(c, culture, visitedKeys, logger)).ToList(),
                IPublishedContent singlePick => [MapPublishedContent(singlePick, culture, visitedKeys, logger)],
                _ => [],
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MapComponents failed for property {Alias}, Culture={Culture}", componentsProperty.Alias, culture);
            return [];
        }
    }

    private static ComponentDto MapPublishedContent(IPublishedElement content, string? culture, HashSet<Guid> visitedKeys, ILogger logger)
    {
        if (!visitedKeys.Add(content.Key))
        {
            return new ComponentDto { Alias = content.ContentType.Alias, Properties = new Dictionary<string, object?>() };
        }

        return new ComponentDto
        {
            Alias = content.ContentType.Alias,
            Properties = MapProperties(content, culture, visitedKeys, logger),
        };
    }

    private static Dictionary<string, object?> MapProperties(IPublishedElement content, string? culture, HashSet<Guid> visitedKeys, ILogger logger)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in content.Properties)
        {
            try
            {
                var value = property.GetValue(culture) ?? property.GetValue(null);
                result[property.Alias] = SanitizeValue(value, culture, visitedKeys, logger);
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

    private static object? SanitizeValue(object? value, string? culture, HashSet<Guid> visitedKeys, ILogger logger) => value switch
    {
        null => null,
        string or bool or int or long or double or decimal or Guid or DateTime or DateTimeOffset => value,
        BlockGridModel blockGrid => blockGrid.Select(i => MapPublishedContent(i.Content, culture, visitedKeys, logger)).ToList(),
        BlockListModel blockList => blockList.Select(i => MapPublishedContent(i.Content, culture, visitedKeys, logger)).ToList(),
        IEnumerable<IPublishedContent> list => list.Select(c => MapPublishedContent(c, culture, visitedKeys, logger)).ToList(),
        IPublishedContent content => MapPublishedContent(content, culture, visitedKeys, logger),
        _ => value.ToString(),
    };
}
