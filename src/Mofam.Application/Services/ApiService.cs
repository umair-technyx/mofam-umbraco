using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using Microsoft.Extensions.Options;
using Serilog;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class ApiServcie(
    ISiteRootResolver siteRootResolver,
    IComponentMapper componentMapper,
    IPropertyValueMapper valueMapper,
    ISeoMapper seoMapper,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : IApiService
{
    private const string CacheKeyPrefix = "mofam:page:";

    public PageDto? GetPageBySlug(string pageContentTypeAlias, string slug, string? culture)
    {
        var cacheKey = $"{CacheKeyPrefix}{pageContentTypeAlias}:{culture}:{Normalise(slug)}";

        return cachePolicy.GetOrCreate(
            cacheKey,
            cacheOptions.Value.Page,
            () => BuildPage(pageContentTypeAlias, slug, culture));
    }

    private PageDto? BuildPage(string pageContentTypeAlias, string slug, string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            var cultureKey = culture ?? string.Empty;
            var rootAlias = CmsConstants.ContentTypes.RootFor(pageContentTypeAlias);
            var wanted = Normalise(slug);

            if (wanted is null) return null;

            var channelRoot = siteRootResolver.GetRoot(rootAlias);
            if (channelRoot is null) return null;

            // Matches the editor-controlled "slug" property, NOT Umbraco's generated
            // UrlSegment — the two diverge as soon as an editor sets a slug that differs
            // from the node name.
            // NOTE: obsolete Children property — see SiteRootResolver for the Umbraco 18 migration note.
#pragma warning disable CS0618
            var matches = channelRoot.Children
                .Where(c =>
                    c.ContentType.Alias == pageContentTypeAlias &&
                    c.IsPublished(culture) &&
                    string.Equals(SlugOf(c, culture), wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();
#pragma warning restore CS0618

            if (matches.Count == 0) return null;

            // Umbraco enforces uniqueness on UrlSegment, but not on a custom text field.
            if (matches.Count > 1)
            {
                logger.Warning(
                    "Duplicate slug '{Slug}' on {Count} '{ContentType}' nodes for culture {Culture} — serving the first",
                    wanted, matches.Count, pageContentTypeAlias, culture);
            }

            var page = matches[0];
            page.Cultures.TryGetValue(cultureKey, out var pageCultureInfo);

            var componentsProperty = page.GetProperty(CmsConstants.Fields.Components);

            return new PageDto
            {
                Id = page.Key.ToString(),
                Slug = SlugOf(page, culture) ?? wanted,
                Title = pageCultureInfo?.Name ?? page.Name ?? string.Empty,
                Seo = seoMapper.Map(page, culture),
                Components = componentMapper.MapComponents(componentsProperty, culture),
            };
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
