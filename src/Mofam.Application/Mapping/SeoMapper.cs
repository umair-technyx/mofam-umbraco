using Mofam.Application.Abstractions;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Mapping;

public sealed class SeoMapper(IPropertyValueMapper valueMapper) : ISeoMapper
{
    public SeoDto Map(IPublishedElement content, string? culture) => new()
    {
        Title = Text(content, CmsConstants.SeoFields.MetaTitle, culture),
        Description = Text(content, CmsConstants.SeoFields.MetaDescription, culture),
        Keywords = Text(content, CmsConstants.SeoFields.MetaKeywords, culture),
        CanonicalUrl = Text(content, CmsConstants.SeoFields.MetaCanonicalLink, culture),
        SchemaJson = Text(content, CmsConstants.SeoFields.MetaSchemaJson, culture),

        Robots = new RobotsDto
        {
            Index = Flag(content, CmsConstants.SeoFields.RobotsIndex, culture),
            Follow = Flag(content, CmsConstants.SeoFields.RobotsFollow, culture),
        },

        OpenGraph = new OpenGraphDto
        {
            Title = Text(content, CmsConstants.SeoFields.OgTitle, culture),
            Description = Text(content, CmsConstants.SeoFields.OgDescription, culture),
            Type = Text(content, CmsConstants.SeoFields.OgType, culture),
            Url = Text(content, CmsConstants.SeoFields.OgPageUrl, culture),
            // The doctype carries two media pickers for the same purpose; prefer ogImage.
            Image = valueMapper.Media(content, CmsConstants.SeoFields.OgImage, culture)
                    ?? valueMapper.Media(content, CmsConstants.SeoFields.OpenGraphImage, culture),
            ExternalImageUrl = Text(content, CmsConstants.SeoFields.OpenGraphImageExternal, culture),
        },

        Twitter = new TwitterCardDto
        {
            Title = Text(content, CmsConstants.SeoFields.TwitterTitle, culture),
            Description = Text(content, CmsConstants.SeoFields.TwitterDescription, culture),
            Url = Text(content, CmsConstants.SeoFields.TwitterUrl, culture),
            Image = valueMapper.Media(content, CmsConstants.SeoFields.TwitterImage, culture),
        },

        Hreflang = BuildHreflang(content, culture),

        Sitemap = new SitemapDto
        {
            Hide = Flag(content, CmsConstants.SeoFields.HideFromSitemap, culture),
            Priority = Int(content, CmsConstants.SeoFields.CustomPriority, culture),
            ChangeFrequency = FirstOf(content, CmsConstants.SeoFields.CustomChangeFrequency, culture),
            LastUpdated = Date(content, CmsConstants.SeoFields.LastUpdatedDate, culture),
        },
    };

    /// <summary>
    /// Only entries with a URL are emitted, so the front end can render the list
    /// directly without filtering out blanks.
    /// </summary>
    private IReadOnlyList<HreflangDto> BuildHreflang(IPublishedElement content, string? culture)
    {
        (string Code, string Alias)[] sources =
        [
            ("x-default", CmsConstants.SeoFields.HreflangDefault),
            (CmsConstants.Cultures.English, CmsConstants.SeoFields.HreflangEnglish),
            (CmsConstants.Cultures.Arabic, CmsConstants.SeoFields.HreflangArabic),
        ];

        var result = new List<HreflangDto>();

        foreach (var (code, alias) in sources)
        {
            var href = Text(content, alias, culture);
            if (!string.IsNullOrWhiteSpace(href))
            {
                result.Add(new HreflangDto { Hreflang = code, Href = href });
            }
        }

        return result;
    }

    // Blank strings are noise in a JSON payload; normalise them to null.
    private string? Text(IPublishedElement content, string alias, string? culture)
    {
        var value = valueMapper.Text(content, alias, culture);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool Flag(IPublishedElement content, string alias, string? culture) =>
        valueMapper.Raw(content, alias, culture) is bool flag && flag;

    private int? Int(IPublishedElement content, string alias, string? culture) =>
        valueMapper.Raw(content, alias, culture) switch
        {
            int i => i,
            long l => (int)l,
            _ => null,
        };

    private DateTime? Date(IPublishedElement content, string alias, string? culture) =>
        valueMapper.Raw(content, alias, culture) switch
        {
            // Umbraco returns DateTime.MinValue for an unset date picker.
            DateTime d when d != default => d,
            _ => null,
        };

    private string? FirstOf(IPublishedElement content, string alias, string? culture) =>
        valueMapper.Raw(content, alias, culture) switch
        {
            IEnumerable<string> many => many.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            string one when !string.IsNullOrWhiteSpace(one) => one,
            _ => null,
        };
}
