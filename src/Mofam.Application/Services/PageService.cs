using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Services;

public sealed class PageService(
    IPublishedContentQuery contentQuery,
    IComponentMapper componentMapper,
    ILogger logger) : IPageService
{
    public PageDto? GetPageBySlug(string rootAlias, string pageContentTypeAlias, string slug, string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            var cultureKey = culture ?? string.Empty;

            var channelRoot = contentQuery
                .ContentAtRoot()
                .SelectMany(r => r.Children)
                .FirstOrDefault(c => c.ContentType.Alias == rootAlias);

            if (channelRoot is null) return null;

            var page = channelRoot.Children
                .FirstOrDefault(c =>
                    c.ContentType.Alias == pageContentTypeAlias &&
                    c.Cultures.TryGetValue(cultureKey, out var info) &&
                    info.UrlSegment == slug &&
                    c.IsPublished(culture));

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
                "GetPageBySlug failed. RootAlias={RootAlias}, PageContentTypeAlias={PageContentTypeAlias}, Slug={Slug}, Culture={Culture}",
                rootAlias, pageContentTypeAlias, slug, culture);
            throw;
        }
    }
}
