using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Serilog;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class PageService(
    ISiteRootResolver siteRootResolver,
    IComponentMapper componentMapper,
    ILogger logger) : IPageService
{
    public PageDto? GetPageBySlug(string pageContentTypeAlias, string slug, string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            var cultureKey = culture ?? string.Empty;
            var rootAlias = CmsConstants.ContentTypes.RootFor(pageContentTypeAlias);

            var channelRoot = siteRootResolver.GetRoot(rootAlias);
            if (channelRoot is null) return null;

            // NOTE: obsolete Children property — see SiteRootResolver for the Umbraco 18 migration note.
#pragma warning disable CS0618
            var page = channelRoot.Children
                .FirstOrDefault(c =>
                    c.ContentType.Alias == pageContentTypeAlias &&
                    c.Cultures.TryGetValue(cultureKey, out var info) &&
                    info.UrlSegment == slug &&
                    c.IsPublished(culture));
#pragma warning restore CS0618

            if (page is null) return null;

            page.Cultures.TryGetValue(cultureKey, out var pageCultureInfo);

            var componentsProperty = page.GetProperty(CmsConstants.Fields.Components);

            return new PageDto
            {
                Id = page.Key.ToString(),
                Slug = pageCultureInfo?.UrlSegment ?? slug,
                Title = pageCultureInfo?.Name ?? page.Name ?? string.Empty,
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
}
