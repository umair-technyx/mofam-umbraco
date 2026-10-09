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

    #region Filters

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
                // Scan the content tree once for all option types, not once per filter group.
                var nodesByType = PublishedItemsOfTypes(sources.Select(s => s.ItemContentTypeAlias), culture);

                var filters = sources
                    .Select(source => new FilterGroupDto
                    {
                        Key = source.Key,
                        Values = OptionsFor(nodesByType.GetValueOrDefault(source.ItemContentTypeAlias) ?? [], culture),
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

    #endregion

    #region Options

    // Every published node of the option type, even if nothing uses it.
    private List<FilterValueDto> OptionsFor(List<IPublishedContent> nodes, string? culture)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            var label = valueMapper.Text(node, CmsConstants.Fields.Title, culture);

            // No fallback to the node name on purpose. An option with no title in this
            // culture is not translated, so we skip it instead of showing the wrong language.
            // Because of this, the check below is required.
            if (string.IsNullOrWhiteSpace(label)) continue;

            options.TryAdd(node.Key.ToString(), label);
        }

        return options
            .OrderBy(o => o.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(o => new FilterValueDto { Key = o.Key, Value = o.Value })
            .ToList();
    }

    #endregion

    #region Published content

    // Walks the published cache instead of Examine, so filters do not depend on the index.
    // Walks the tree once and groups the published nodes by content type alias.
    private Dictionary<string, List<IPublishedContent>> PublishedItemsOfTypes(
        IEnumerable<string> contentTypeAliases, string? culture)
    {
        var results = new Dictionary<string, List<IPublishedContent>>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in contentTypeAliases)
        {
            results[alias] = [];
        }

        foreach (var root in contentQuery.ContentAtRoot())
        {
            Collect(root);
        }

        return results;

        void Collect(IPublishedContent node)
        {
            if (results.TryGetValue(node.ContentType.Alias, out var matches) && node.IsPublished(culture))
            {
                matches.Add(node);
            }

            // Children is obsolete. See SiteRootResolver for the Umbraco 18 note.
#pragma warning disable CS0618
            foreach (var child in node.Children)
#pragma warning restore CS0618
            {
                Collect(child);
            }
        }
    }

    #endregion
}
