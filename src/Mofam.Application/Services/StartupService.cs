using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Application.Mapping;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using Microsoft.Extensions.Options;
using Serilog;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class StartupService(
    ISiteRootResolver siteRootResolver,
    IPropertyValueMapper valueMapper,
    IDictionaryItemService dictionaryItemService,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : IStartupService
{
    private const string CacheKeyPrefix = "mofam:startup:";

    public Task<Startup?> GetStartupAsync(string? culture) =>
        cachePolicy.GetOrCreateAsync(
            $"{CacheKeyPrefix}{culture}",
            cacheOptions.Value.Startup,
            () => BuildStartupAsync(culture));

    private async Task<Startup?> BuildStartupAsync(string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        try
        {
            var site = siteRootResolver.GetRoot(CmsConstants.RootAlias.Site);
            if (site is null)
            {
                logger.Warning("Startup requested but no '{RootAlias}' root was found", CmsConstants.RootAlias.Site);
                return null;
            }

            return new Startup
            {
                Culture = culture,
                Header = BuildHeader(site, culture),
                Footer = BuildFooter(site, culture),
                Dictionary = await GetDictionaryAsync(culture),
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "GetStartupAsync failed. Culture={Culture}", culture);
            throw;
        }
    }

    // ---------- header / footer ----------

    private HeaderDto BuildHeader(IPublishedContent site, string? culture) => new()
    {
        PrimaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.HeaderPrimaryLogo, culture),
        SecondaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.HeaderSecondaryLogo, culture),
        Navigation = Navigation(site, CmsConstants.SiteFields.Header, culture),
    };

    private FooterDto BuildFooter(IPublishedContent site, string? culture) => new()
    {
        PrimaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.FooterPrimaryLogo, culture),
        SecondaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.FooterSecondaryLogo, culture),
        Navigation = Navigation(site, CmsConstants.SiteFields.Footer, culture),
        SocialLinks = valueMapper.Links(site, CmsConstants.SiteFields.SocialLinks, culture),
        FollowUsLabel = valueMapper.Text(site, CmsConstants.SiteFields.LabelFollowUs, culture),
        BottomText = valueMapper.Text(site, CmsConstants.SiteFields.BottomText, culture),
    };

    private IReadOnlyList<NavigationItemDto> Navigation(IPublishedContent site, string alias, string? culture)
    {
        if (valueMapper.Raw(site, alias, culture) is not IEnumerable<IPublishedContent> nodes) return [];

        return nodes
            .Where(n => n.IsPublished(culture))
            .Select(n => MapNavigationItem(n, culture))
            .ToList();
    }

    private NavigationItemDto MapNavigationItem(IPublishedContent node, string? culture)
    {
        node.Cultures.TryGetValue(culture ?? string.Empty, out var cultureInfo);

        // Navigation item types each name their link property differently, so match by
        // value type rather than hard-coding four aliases.
        var link = LinkMapper.FirstLinkOn(node, culture);

        // NOTE: obsolete Children property — see SiteRootResolver for the Umbraco 18 migration note.
#pragma warning disable CS0618
        var children = node.Children
            .Where(c => c.IsPublished(culture))
            .Select(c => MapNavigationItem(c, culture))
            .ToList();
#pragma warning restore CS0618

        return new NavigationItemDto
        {
            Name = cultureInfo?.Name ?? node.Name ?? string.Empty,
            Link = link,
            Children = children,
        };
    }

    // ---------- dictionary ----------

    private async Task<IReadOnlyDictionary<string, string>> GetDictionaryAsync(string? culture)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var roots = await dictionaryItemService.GetAtRootAsync();

            foreach (var root in roots)
            {
                Add(root);

                var descendants = await dictionaryItemService.GetDescendantsAsync(root.Key);
                foreach (var descendant in descendants)
                {
                    Add(descendant);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load dictionary items for culture {Culture}", culture);
        }

        return result;

        void Add(IDictionaryItem item)
        {
            var value = Translate(item, culture);
            if (!string.IsNullOrEmpty(value))
            {
                result[item.ItemKey] = value;
            }
        }
    }

    /// <summary>
    /// Exact ISO match first, then a language-prefix match so "en" still resolves an
    /// "en-US" translation, then whatever exists.
    /// </summary>
    private static string? Translate(IDictionaryItem item, string? culture)
    {
        var translations = item.Translations?.ToList() ?? [];
        if (translations.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(culture))
        {
            var exact = translations.FirstOrDefault(t =>
                string.Equals(t.LanguageIsoCode, culture, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact.Value;

            var language = culture.Split('-')[0];
            var prefixed = translations.FirstOrDefault(t =>
                t.LanguageIsoCode?.StartsWith(language, StringComparison.OrdinalIgnoreCase) == true);
            if (prefixed is not null) return prefixed.Value;
        }

        return translations[0].Value;
    }
}
