using Mofam.Application.Abstractions;
using Mofam.Domain.Models.Dtos;
using Serilog;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class MediaUrlBuilder(
    IPublishedUrlProvider urlProvider,
    IPublishedValueFallback publishedValueFallback,
    ILogger logger) : IMediaUrlBuilder
{
    private const string WidthAlias = "umbracoWidth";
    private const string HeightAlias = "umbracoHeight";
    private const string BytesAlias = "umbracoBytes";
    private const string ExtensionAlias = "umbracoExtension";
    private const string AltTextAlias = "altText";

    public bool IsMedia(object? value) => value switch
    {
        IEnumerable<IPublishedContent> many => many.Any(m => m.ItemType == PublishedItemType.Media),
        IPublishedContent one => one.ItemType == PublishedItemType.Media,
        _ => false,
    };

    public MediaDto? Build(IPublishedContent? media, string? culture = null)
    {
        if (media is null || media.ItemType != PublishedItemType.Media) return null;

        try
        {
            var url = urlProvider.GetMediaUrl(media, UrlMode.Default, culture);
            if (string.IsNullOrWhiteSpace(url)) return null;

            return new MediaDto
            {
                Url = url,
                Name = media.Name,
                AltText = Value<string>(media, AltTextAlias, culture),
                Extension = Value<string>(media, ExtensionAlias, culture),
                Width = Value<int?>(media, WidthAlias, culture),
                Height = Value<int?>(media, HeightAlias, culture),
                Bytes = Value<long?>(media, BytesAlias, culture),
            };
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to build media URL for {MediaKey} ({Name})", media.Key, media.Name);
            return null;
        }
    }

    public IReadOnlyList<MediaDto> BuildMany(IEnumerable<IPublishedContent>? media, string? culture = null)
    {
        if (media is null) return [];

        return media
            .Select(m => Build(m, culture))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();
    }

    // Media property aliases are optional and vary by media type, so a miss is normal.
    private T? Value<T>(IPublishedContent media, string alias, string? culture)
    {
        try
        {
            return media.HasProperty(alias)
                ? media.Value<T>(publishedValueFallback, alias, culture)
                : default;
        }
        catch
        {
            return default;
        }
    }
}
