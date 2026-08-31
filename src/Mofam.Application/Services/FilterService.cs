using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class FilterService(
    IPublishedContentQuery contentQuery,
    IPropertyValueMapper valueMapper,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : IFilterService
{
    private const string CacheKeyPrefix = "mofam:filters:";

    public FilterDataDto? GetFilterData(string contentType, string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        if (string.IsNullOrWhiteSpace(contentType)) return null;

        if (!SearchConstants.FilterDefinitions.TryGetValue(contentType, out var sources) || sources.Length == 0)
        {
            logger.Warning("No filters defined for content type {ContentType}", contentType);
            return null;
        }

        var cacheKey = $"{CacheKeyPrefix}{contentType}:{culture}";

        try
        {
            return cachePolicy.GetOrCreate(cacheKey, cacheOptions.Value.Filters, () =>
            {
                var filters = sources
                    .Select(source => new FilterGroupDto
                    {
                        Key = source.Key,
                        Values = OptionsFor(source.ItemContentTypeAlias, culture),
                    })
                    .Where(group => group.Values.Count > 0)
                    .ToList();

                return new FilterDataDto
                {
                    ContentType = contentType,
                    Filters = filters,
                };
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "GetFilterData failed. ContentType={ContentType}, Culture={Culture}", contentType, culture);
            return null;
        }
    }

    /// <summary>
    /// Every published node of the option type, whether or not anything references it.
    /// </summary>
    private List<FilterValueDto> OptionsFor(string itemContentTypeAlias, string? culture)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in PublishedItemsOfType(itemContentTypeAlias, culture))
        {
            var label = valueMapper.Text(node, CmsConstants.Fields.Title, culture);

            // No node-name fallback by design: an option with no title in the requested
            // culture is untranslated, so it is skipped rather than shown in the wrong
            // language. Skipping means the guard below is required, not optional.
            if (string.IsNullOrWhiteSpace(label)) continue;

            options.TryAdd(node.Key.ToString(), label);
        }

        return options
            .OrderBy(o => o.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(o => new FilterValueDto { Key = o.Key, Value = o.Value })
            .ToList();
    }

    /// <summary>
    /// Walks the published cache rather than Examine, so filters work independently of
    /// index state. The result is cached.
    /// </summary>
    private List<IPublishedContent> PublishedItemsOfType(string contentTypeAlias, string? culture)
    {
        var results = new List<IPublishedContent>();

        foreach (var root in contentQuery.ContentAtRoot())
        {
            Collect(root);
        }

        return results;

        void Collect(IPublishedContent node)
        {
            if (string.Equals(node.ContentType.Alias, contentTypeAlias, StringComparison.OrdinalIgnoreCase)
                && node.IsPublished(culture))
            {
                results.Add(node);
            }

            // NOTE: obsolete Children property — see SiteRootResolver for the Umbraco 18 migration note.
#pragma warning disable CS0618
            foreach (var child in node.Children)
#pragma warning restore CS0618
            {
                Collect(child);
            }
        }
    }
}
