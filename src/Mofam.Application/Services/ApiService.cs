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
    PageCacheToken pageCacheToken,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : IApiService
{
    private const string CacheKeyPrefix = "mofam:page:";

    #region Get page

    public PageDto? GetPageBySlug(string contentTypeAlias, string slug, string? culture)
    {
        // Lower case, so "Page" and "page" (or "EN" and "en") share one cache entry.
        var cacheKey = $"{CacheKeyPrefix}{contentTypeAlias.ToLowerInvariant()}:{culture?.ToLowerInvariant()}:{CommonHelper.NormaliseSlug(slug, shortStringHelper)}";

        // The page cache is linked to a shared token, so a publish in the CMS clears all pages at once.
        return cachePolicy.GetOrCreate(
            cacheKey,
            cacheOptions.Value.Page,
            () => BuildPage(contentTypeAlias, slug, culture),
            pageCacheToken.Token);
    }

    #endregion

    #region Build page

    // Finds a page by content type and slug using the Examine index, the same way the search does.
    // The index finds the node wherever it is in the tree, so moving a node in the CMS does not break this.
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

            // Umbraco does not check that a custom slug field is unique, so two nodes can share a slug.
            if (matches.Count > 1)
            {
                logger.Warning(
                    "Duplicate slug '{Slug}' on {Count} '{ContentType}' nodes for culture {Culture} — serving the first",
                    wanted, matches.Count, pageContentTypeAlias, culture);
            }

            if (!int.TryParse(matches[0].Id, out var nodeId)) return null;

            var content = contentQuery.Content(nodeId);

            // The index can be a little behind the published cache (for example, a page was just
            // unpublished), so check the node is still published.
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

    #endregion
}
