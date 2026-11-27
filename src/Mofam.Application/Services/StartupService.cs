using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Application.Mapping;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class StartupService(
    ISiteRootResolver siteRootResolver,
    IPropertyValueMapper valueMapper,
    IPublishedContentQuery contentQuery,
    IDictionaryItemService dictionaryItemService,
    ICachePolicy cachePolicy,
    PageCacheToken pageCacheToken,
    IOptions<CacheOptions> cacheOptions,
    IVariationContextAccessor variationContextAccessor,
    ILogger logger) : IStartupService
{
    public const string CacheKeyPrefix = "mofam:startup:";

    #region Startup

    // Linked to the publish token, so a publish, a dictionary change or a media change clears it at once.
    public Task<Startup?> GetStartupAsync(string? culture) => cachePolicy.GetOrCreateAsync($"{CacheKeyPrefix}{culture}", cacheOptions.Value.Startup, () => BuildStartupAsync(culture), pageCacheToken.Token);

    private async Task<Startup?> BuildStartupAsync(string? culture)
    {
        using var tracer = new FunctionTracer(loginfile: true);

        var site = siteRootResolver.GetRoot(CmsConstants.RootAlias.Site);
        if (site is null)
        {
            logger.Warning("Startup requested but no '{RootAlias}' root was found", CmsConstants.RootAlias.Site);
            return null;
        }

        // Links picked from a content node get their URL in the current culture. Without this the Arabic header
        // and footer links would be built in the default culture and come out with the /en prefix.
        if (!string.IsNullOrWhiteSpace(culture))
        {
            variationContextAccessor.VariationContext = new VariationContext(culture);
        }

        // A failure is not caught here: GlobalExceptionMiddleware logs it once, with the request path.
        return new Startup
        {
            Culture = culture,
            Header = BuildHeader(site, culture),
            Footer = BuildFooter(site, culture),
            Scripts = BuildScripts(site, culture),
            Dictionary = await GetDictionaryAsync(culture),
            ContentTypes = CmsConstants.ContentTypes.PageTypes,
            GlobalSearch = GlobalSearchLinks(site, culture),
            NewsletterForm = BuildNewsletterForm(site, culture),
        };
    }

    #endregion

    #region Newsletter form

    // The Newsletter Form Content tab of the site root. Null when nothing is filled in.
    private NewsletterFormDto? BuildNewsletterForm(IPublishedContent site, string? culture)
    {
        var form = new NewsletterFormDto
        {
            LeftCardHeading = Blank(valueMapper.Text(site, CmsConstants.SiteFields.LeftCardHeading, culture)),
            LeftCardTitle = Blank(valueMapper.Text(site, CmsConstants.SiteFields.LeftCardTitle, culture)),
            LeftCardDescription = Blank(valueMapper.Text(site, CmsConstants.SiteFields.LeftCardDescription, culture)),
            LeftCardInputPlaceholder = Blank(valueMapper.Text(site, CmsConstants.SiteFields.LeftCardInputPlaceholder, culture)),
            LeftCardCtaLabel = Blank(valueMapper.Text(site, CmsConstants.SiteFields.LeftCardCtaLabel, culture)),
            RightCardHeading = Blank(valueMapper.Text(site, CmsConstants.SiteFields.RightCardHeading, culture)),
            RightCardTitle = Blank(valueMapper.Text(site, CmsConstants.SiteFields.RightCardTitle, culture)),
            RightCardPrimaryCTA = valueMapper.Links(site, CmsConstants.SiteFields.RightCardPrimaryCta, culture).FirstOrDefault(),
            RightCardSecondaryCTA = valueMapper.Links(site, CmsConstants.SiteFields.RightCardSecondaryCta, culture).FirstOrDefault(),
            LeftDecorativeElements = DecorativeElements(site, CmsConstants.SiteFields.LeftDecorativeElements, culture),
            RightDecorativeElements = DecorativeElements(site, CmsConstants.SiteFields.RightDecorativeElements, culture),
        };

        return form == new NewsletterFormDto() ? null : form;
    }

    // The decorative elements in the same shape as in a component: { alias, properties }.
    private IReadOnlyList<ComponentDto>? DecorativeElements(IPublishedContent site, string alias, string? culture)
    {
        if (valueMapper.Raw(site, alias, culture) is not BlockListModel blocks || blocks.Count == 0) return null;

        var properties = valueMapper.BlockListItems(site, alias, culture);
        return [.. blocks.Select((block, i) => new ComponentDto { Alias = block.Content.ContentType.Alias, Properties = properties[i] })];
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    #endregion

    #region Header and Footer

    private HeaderDto BuildHeader(IPublishedContent site, string? culture) => new()
    {
        TopBarLabel = valueMapper.Text(site, CmsConstants.SiteFields.TopBarLabel, culture),
        LearnMoreCta = valueMapper.Links(site, CmsConstants.SiteFields.LearnMoreCta, culture).FirstOrDefault(),
        Navigation = Navigation(site, CmsConstants.SiteFields.Header, culture),
        PrimaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.HeaderPrimaryLogo, culture),
        SecondaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.HeaderSecondaryLogo, culture),
        LogoAltText = valueMapper.Text(site, CmsConstants.SiteFields.HeaderLogoAltText, culture),
        EmailLabel = valueMapper.Text(site, CmsConstants.SiteFields.HeaderEmailLabel, culture),
        EmailInputText = valueMapper.Text(site, CmsConstants.SiteFields.HeaderEmailInputText, culture),
        PhoneLabel = valueMapper.Text(site, CmsConstants.SiteFields.HeaderPhoneLabel, culture),
        PhoneInputText = valueMapper.Text(site, CmsConstants.SiteFields.HeaderPhoneInputText, culture),
        SocialIconsLabel = valueMapper.Text(site, CmsConstants.SiteFields.HeaderSocialIconsLabel, culture),
        SocialIcons = valueMapper.BlockListItems(site, CmsConstants.SiteFields.HeaderSocialIcons, culture),

    };

    private FooterDto BuildFooter(IPublishedContent site, string? culture) => new()
    {
        Navigation = Navigation(site, CmsConstants.SiteFields.Footer, culture),
        PrimaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.FooterPrimaryLogo, culture),
        SecondaryLogo = valueMapper.Media(site, CmsConstants.SiteFields.FooterSecondaryLogo, culture),
        LogoAltText = valueMapper.Text(site, CmsConstants.SiteFields.FooterLogoAltText, culture),
        BottomText = ApplyLastUpdated(valueMapper.Text(site, CmsConstants.SiteFields.BottomText, culture), culture),
        FollowUsLabel = valueMapper.Text(site, CmsConstants.SiteFields.FollowUsLabel, culture),
        SocialLinks = valueMapper.BlockListItems(site, CmsConstants.SiteFields.SocialLinks, culture),
    };

    #endregion

    #region Global search

    // The life stage links of the Global Search Content tab: a block list of icon + CTA.
    // When the block list is empty, the older plain link picker is used, so nothing disappears while the CMS is being filled.
    private IReadOnlyList<SearchItemDto> GlobalSearchLinks(IPublishedContent site, string? culture)
    {
        var blocks = LifeStageLinkBlocks(site, culture);
        return blocks.Count > 0 ? blocks : PickedHelpLinks(site, culture);
    }

    private IReadOnlyList<SearchItemDto> LifeStageLinkBlocks(IPublishedContent site, string? culture)
    {
        if (valueMapper.Raw(site, CmsConstants.SiteFields.LifeStagesLinks, culture) is not BlockListModel blocks) return [];

        var items = new List<SearchItemDto>();
        foreach (var block in blocks)
        {
            var cta = valueMapper.Links(block.Content, CmsConstants.LifeStageLinkBlock.Cta, culture).FirstOrDefault();
            if (cta is null || string.IsNullOrWhiteSpace(cta.Name) || string.IsNullOrWhiteSpace(cta.Url)) continue;

            var link = valueMapper.Raw(block.Content, CmsConstants.LifeStageLinkBlock.Cta, culture) is IEnumerable<Link> many ? many.FirstOrDefault() : null;
            items.Add(new SearchItemDto
            {
                Title = cta.Name,
                Slug = SlugOf(link, culture),
                Icon = valueMapper.Media(block.Content, CmsConstants.LifeStageLinkBlock.Icon, culture),
                Cta = cta,
            });
        }

        return items;
    }

    private string? SlugOf(Link? link, string? culture)
    {
        var node = link is { Type: LinkType.Content, Udi: GuidUdi udi } ? contentQuery.Content(udi.Guid) : null;
        var slug = node is null ? null : valueMapper.Text(node, CmsConstants.Fields.Slug, culture);
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    // The links picked in the Global Search Content tab. A link without a title or an address is skipped.
    private IReadOnlyList<SearchItemDto> PickedHelpLinks(IPublishedContent site, string? culture)
    {
        if (valueMapper.Raw(site, CmsConstants.SiteFields.HelpLinks, culture) is not IEnumerable<Link> links) return [];

        var items = new List<SearchItemDto>();
        foreach (var link in links)
        {
            var title = string.IsNullOrWhiteSpace(link.Name) ? null : link.Name;
            if (title is null || string.IsNullOrWhiteSpace(link.Url)) continue;

            items.Add(new SearchItemDto { Title = title, Slug = SlugOf(link, culture), Cta = LinkMapper.Map(link) });
        }

        return items;
    }

    #endregion

    #region Scripts
    private ScriptsDto BuildScripts(IPublishedContent site, string? culture) => new()
    {
        AccessibilityScript = valueMapper.Text(site, CmsConstants.SiteFields.AccessibilityScript, culture),
    };

    #endregion

    #region Last updated date

    // Replaces {lastUpdated} in the text with the date of the latest published content.
    // The date uses the format of the culture: English digits for en, Arabic digits for ar.
    private string? ApplyLastUpdated(string? text, string? culture)
    {
        if (string.IsNullOrEmpty(text)
            || !text.Contains(CmsConstants.SiteFields.LastUpdatedToken, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var date = FormatDate(GetLastPublishedDate(), culture);

        return text.Replace(CmsConstants.SiteFields.LastUpdatedToken, date, StringComparison.OrdinalIgnoreCase);
    }

    // Latest update date of any published content. Reads the in-memory published cache only.
    private DateTime GetLastPublishedDate()
    {
        var latest = DateTime.MinValue;

        foreach (var root in contentQuery.ContentAtRoot())
        {
            Visit(root);
        }

        return latest.Kind == DateTimeKind.Utc ? latest.ToLocalTime() : latest;

        void Visit(IPublishedContent node)
        {
            if (node.UpdateDate > latest) latest = node.UpdateDate;

            // Children is obsolete. See SiteRootResolver for the Umbraco 18 note.
#pragma warning disable CS0618
            foreach (var child in node.Children)
#pragma warning restore CS0618
            {
                Visit(child);
            }
        }
    }

    // en: 24/03/2026 06:41 PM    ar: ٢٤/٠٣/٢٠٢٦ ٠٦:٤١ م
    private static string FormatDate(DateTime date, string? culture)
    {
        var isArabic = string.Equals(culture, CmsConstants.Cultures.Arabic, StringComparison.OrdinalIgnoreCase);
        var text = date.ToString("dd/MM/yyyy hh:mm tt", CultureInfo.GetCultureInfo(isArabic ? CmsConstants.Cultures.Arabic : CmsConstants.Cultures.English));

        // The Arabic culture adds hidden direction marks (U+200F / U+200E) around "/". Remove them.
        text = text.Replace("‏", string.Empty).Replace("‎", string.Empty);

        return isArabic ? ToArabicDigits(text) : text;
    }

    private static string ToArabicDigits(string text) =>
        string.Create(text.Length, text, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = source[i] is >= '0' and <= '9' ? (char)('٠' + (source[i] - '0')) : source[i];
            }
        });

    #endregion

    #region Navigation

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

        // Each navigation item type has a different link property name,
        // so we find the link by its type instead of its name.
        var link = LinkMapper.FirstLinkOn(node, culture);

        // Children is obsolete. See SiteRootResolver for the Umbraco 18 note.
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

    #endregion

    #region Dictionary

    private async Task<IReadOnlyDictionary<string, string>> GetDictionaryAsync(string? culture)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Passing null gets all dictionary items in one query.
            var items = await dictionaryItemService.GetDescendantsAsync(null);

            foreach (var item in items)
            {
                Add(item);
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


    // Find the translation for the culture. Try the exact code first (en-US),
    // then the language only (en), and if nothing matches use the first one.
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
    #endregion
}
