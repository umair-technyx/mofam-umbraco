using Examine;
using Examine.Search;
using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class ApiServcie(
    IExamineManager examineManager,
    IPublishedContentQuery contentQuery,
    IPageMapper pageMapper,
    IShortStringHelper shortStringHelper,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : IApiService
{
    private const string CacheKeyPrefix = "mofam:page:";

    public PageDto? GetPageBySlug(string contentTypeAlias, string slug, string? culture)
    {
        var cacheKey = $"{CacheKeyPrefix}{contentTypeAlias}:{culture}:{CommonHelper.NormaliseSlug(slug, shortStringHelper)}";

        return cachePolicy.GetOrCreate(
            cacheKey,
            cacheOptions.Value.Page,
            () => BuildPage(contentTypeAlias, slug, culture));
    }

    /// <summary>
    /// Resolves a page by content type + slug through the Examine index, the same way
    /// <see cref="SiteSearchService"/> resolves listing/search hits — one lookup mechanism
    /// for the whole API, rather than this endpoint alone walking the content tree from a
    /// hardcoded channel root. That tree-walk assumed every node of a given type lives
    /// directly under one specific container, which breaks the moment an editor reparents
    /// a node (or restructures the tree) — an assumption Examine's index doesn't need,
    /// since it just matches content type + slug wherever the node actually lives.
    /// </summary>
    private PageDto? BuildPage(string pageContentTypeAlias, string slug, string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            var wanted = CommonHelper.NormaliseSlug(slug, shortStringHelper);
            if (wanted is null) return null;

            if (!examineManager.TryGetIndex(SearchConstants.IndexName, out var index))
            {
                logger.Warning(
                    "Examine index {IndexName} is not available — cannot resolve {ContentType}/{Slug}",
                    SearchConstants.IndexName, pageContentTypeAlias, slug);
                return null;
            }

            var publishedField = string.IsNullOrWhiteSpace(culture)
                ? SearchConstants.PublishedField
                : $"{SearchConstants.PublishedField}_{culture}";

            var matches = index.Searcher.CreateQuery(IndexTypes.Content)
                .Field(SearchConstants.NodeTypeAliasField, pageContentTypeAlias)
                .And().Field(publishedField, "y")
                .And().GroupedOr(CommonHelper.ExpandForCulture([CmsConstants.Fields.Slug], culture), [wanted])
                .Execute()
                .ToList();

            if (matches.Count == 0) return null;

            // Umbraco enforces uniqueness on UrlSegment, but not on a custom text field —
            // the index mirrors that same underlying property, so the same caveat applies.
            if (matches.Count > 1)
            {
                logger.Warning(
                    "Duplicate slug '{Slug}' on {Count} '{ContentType}' nodes for culture {Culture} — serving the first",
                    wanted, matches.Count, pageContentTypeAlias, culture);
            }

            if (!int.TryParse(matches[0].Id, out var nodeId)) return null;

            var content = contentQuery.Content(nodeId);

            // Defends against the index briefly lagging the live published-content cache
            // (e.g. a page was just unpublished) — the tree-walk this replaces never had
            // this gap, since it read the live cache directly with no index in between.
            if (content is null || !content.IsPublished(culture)) return null;

            // Detail mode: everything, including detailPageComponents and SEO.
            return pageMapper.Map(content, culture, PageMapMode.Detail);
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "GetPageBySlug failed. PageContentTypeAlias={PageContentTypeAlias}, Slug={Slug}, Culture={Culture}",
                pageContentTypeAlias, slug, culture);
            throw;
        }
    }
}
