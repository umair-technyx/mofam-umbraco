using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Mofam.Domain.Constants;

namespace Mofam.Infrastructure.Routing;

// Builds the URL of every published node, so a link picked anywhere in the CMS (header, footer, a CTA, a block)
// has the shape the front end uses, whatever the hostname settings are:
//  - a page follows the tree, like the sitemap: /{culture}/{parent slugs}/{slug}, e.g. /en/raising-a-family/parenting-programmes;
//  - any other node (service, resource, story, news, media...) sits under its content type: /{culture}/{contentType}/{slug}.
// The slug is the same in every culture, so only the culture part changes. A node without a slug is left to
// the next URL provider, so Umbraco still builds its normal URL.
public sealed class SlugUrlProvider(IVariationContextAccessor variationContextAccessor) : IUrlProvider
{
    public string Alias => "mofamSlug";

    public UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current)
    {
        var slug = SlugOf(content);
        if (string.IsNullOrEmpty(slug)) return null;

        var cultureCode = CultureCode(culture);

        string path;
        if (IsPage(content))
        {
            var parts = new List<string> { slug };

#pragma warning disable CS0618 // Parent is obsolete. See SiteRootResolver for the Umbraco 18 note.
            var parent = content.Parent;
            while (parent is not null && IsPage(parent))
            {
                var parentSlug = SlugOf(parent);
                if (string.IsNullOrEmpty(parentSlug)) break;

                parts.Insert(0, parentSlug);
                parent = parent.Parent;
            }
#pragma warning restore CS0618

            path = string.Join('/', parts);
        }
        else
        {
            path = $"{content.ContentType.Alias}/{slug}";
        }

        return UrlInfo.AsUrl($"/{cultureCode}/{path}", Alias, culture);
    }

    public IEnumerable<UrlInfo> GetOtherUrls(int id, Uri current) => [];

    // There is no preview page on the site itself (the front end is a separate app).
    public Task<UrlInfo?> GetPreviewUrlAsync(IContent content, string? culture, string? segment) => Task.FromResult<UrlInfo?>(null);

    private string CultureCode(string? culture)
    {
        var code = !string.IsNullOrWhiteSpace(culture) ? culture : variationContextAccessor.VariationContext?.Culture;

        return string.IsNullOrWhiteSpace(code) ? CmsConstants.Cultures.English : code.ToLowerInvariant();
    }

    private static bool IsPage(IPublishedContent content) =>
        string.Equals(content.ContentType.Alias, CmsConstants.ContentTypes.Page, StringComparison.OrdinalIgnoreCase);

    private static string? SlugOf(IPublishedContent content)
    {
        var property = content.GetProperty(CmsConstants.Fields.Slug);
        var value = property?.GetValue()?.ToString()?.Trim().Trim('/');

        return string.IsNullOrEmpty(value) ? null : value.ToLowerInvariant();
    }
}
