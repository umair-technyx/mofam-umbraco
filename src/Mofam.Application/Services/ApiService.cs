using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using Serilog;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class ApiServcie(
    ISiteRootResolver siteRootResolver,
    IPageMapper pageMapper,
    IPropertyValueMapper valueMapper,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : IApiService
{
    private const string CacheKeyPrefix = "mofam:page:";

    public PageDto? GetPageBySlug(string contentTypeAlias, string slug, string? culture)
    {
        var cacheKey = $"{CacheKeyPrefix}{contentTypeAlias}:{culture}:{Normalise(slug)}";

        return cachePolicy.GetOrCreate(
            cacheKey,
            cacheOptions.Value.Page,
            () => BuildPage(contentTypeAlias, slug, culture));
    }

    private PageDto? BuildPage(string pageContentTypeAlias, string slug, string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            var rootAlias = CmsConstants.ContentTypes.RootFor(pageContentTypeAlias);
            var wanted = Normalise(slug);

            if (wanted is null) return null;

            var channelRoot = siteRootResolver.GetRoot(rootAlias);
            if (channelRoot is null) return null;

            // Searches the whole subtree, not just direct children: pages sit directly
            // under the site root, but services live inside a "Services" container and
            // are therefore grandchildren.
            var matches = Descendants(channelRoot)
                .Where(c =>
                    c.ContentType.Alias == pageContentTypeAlias &&
                    c.IsPublished(culture) &&
                    string.Equals(SlugOf(c, culture), wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0) return null;

            // Umbraco enforces uniqueness on UrlSegment, but not on a custom text field.
            if (matches.Count > 1)
            {
                logger.Warning(
                    "Duplicate slug '{Slug}' on {Count} '{ContentType}' nodes for culture {Culture} — serving the first",
                    wanted, matches.Count, pageContentTypeAlias, culture);
            }

            // Detail mode: everything, including detailPageComponents and SEO.
            return pageMapper.Map(matches[0], culture, PageMapMode.Detail);
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

    /// <summary>
    /// Walks the subtree beneath <paramref name="root"/>, root excluded. Lazy, so a match
    /// near the top stops the walk rather than enumerating the whole site.
    /// </summary>
    private static IEnumerable<IPublishedContent> Descendants(IPublishedContent root)
    {
        // NOTE: obsolete Children property — see SiteRootResolver for the Umbraco 18 migration note.
#pragma warning disable CS0618
        foreach (var child in root.Children)
#pragma warning restore CS0618
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private string? SlugOf(IPublishedContent content, string? culture) =>
        Normalise(valueMapper.Text(content, CmsConstants.Fields.Slug, culture));

    /// <summary>
    /// Editors paste slugs with stray slashes and whitespace; normalise both sides so
    /// "/about-us/" and "about-us" are the same page.
    /// </summary>
    private static string? Normalise(string? value)
    {
        var trimmed = value?.Trim().Trim('/').Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
