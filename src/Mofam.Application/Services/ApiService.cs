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
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class ApiService(
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
    private const string MissKeyPrefix = "mofam:page-miss:";

    #region Get page

    public PageDto? GetPageBySlug(string contentTypeAlias, string slug, string? culture)
    {
        // Lower case, so "Page" and "page" (or "EN" and "en") share one cache entry.
        var cacheKey = $"{CacheKeyPrefix}{contentTypeAlias.ToLowerInvariant()}:{culture?.ToLowerInvariant()}:{CommonHelper.NormaliseSlug(slug, shortStringHelper)}";
        var missKey = $"{MissKeyPrefix}{cacheKey}";

        // A page that was not found a moment ago is not searched for again.
        if (cachePolicy.TryGet<object>(missKey, out _)) return null;

        // The page cache is linked to a shared token, so a publish in the CMS clears all pages at once.
        // The token is read before the build, so a publish during the build is not missed.
        var token = pageCacheToken.Token;

        var page = cachePolicy.GetOrCreate(
            cacheKey,
            cacheOptions.Value.Page,
            () => BuildPage(contentTypeAlias, slug, culture),
            token);

        // Only short slugs are remembered, so a very long made-up slug cannot fill the memory. A publish
        // clears the entry through the token. If the page is asked for before the index has caught up with the
        // publish, it stays not found until the remembered time has passed.
        if (page is null && slug.Length <= GeneralConstants.MaxRememberedSlugLength)
        {
            cachePolicy.Set<object>(missKey, true, cacheOptions.Value.PageNotFound, token);
        }

        return page;
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

            IPublishedContent? matchedContent = null;
            var exactMatchesCount = 0;

            foreach (var match in matches)
            {
                if (!int.TryParse(match.Id, out var nodeId)) continue;

                var candidate = contentQuery.Content(nodeId);

                // The index can be a little behind the published cache (for example, a page was just
                // unpublished), so check the node is still published.
                if (candidate is null || !candidate.IsPublished(culture)) continue;

                var candidateSlug = GetContentSlug(candidate, culture);
                if (string.Equals(candidateSlug, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatchesCount++;
                    matchedContent ??= candidate;
                }
            }

            if (matchedContent is null) return null;

            // Umbraco does not check that a custom slug field is unique, so two nodes can share a slug.
            if (exactMatchesCount > 1)
            {
                logger.Warning(
                    "Duplicate slug '{Slug}' on {Count} '{ContentType}' nodes for culture {Culture} — serving the first",
                    wanted, exactMatchesCount, pageContentTypeAlias, culture);
            }

            // Detail mode: everything, including detailPageComponents and SEO.
            return pageMapper.Map(matchedContent, culture, PageMapMode.Detail);
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

    private string? GetContentSlug(IPublishedContent content, string? culture)
    {
        var rawSlug = (content.GetProperty(CmsConstants.Fields.Slug)?.GetValue(culture)
                       ?? content.GetProperty(CmsConstants.Fields.Slug)?.GetValue(null))?.ToString();

        var normalized = CommonHelper.NormaliseSlug(rawSlug, shortStringHelper);
        if (!string.IsNullOrEmpty(normalized)) return normalized;

        if (culture is not null && content.Cultures.TryGetValue(culture, out var cultureInfo))
        {
            return CommonHelper.NormaliseSlug(cultureInfo.UrlSegment, shortStringHelper);
        }

        var fallbackSegment = content.Cultures.Values.FirstOrDefault()?.UrlSegment;
        return CommonHelper.NormaliseSlug(fallbackSegment, shortStringHelper);
    }

    #endregion
}
