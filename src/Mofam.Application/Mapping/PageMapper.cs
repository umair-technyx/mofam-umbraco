using Mofam.Application.Abstractions;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Serilog;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Mapping;

public sealed class PageMapper(
    IPropertyValueMapper valueMapper,
    IComponentMapper componentMapper,
    ISeoMapper seoMapper,
    ILogger logger) : IPageMapper
{
    /// <summary>
    /// Aliases that must never enter the extension-data bag.
    /// <para>
    /// Two reasons: the first four are already surfaced as their own fields, and because
    /// the bag serialises inline at the root, ANY collision with a fixed JSON property
    /// makes System.Text.Json throw on a duplicate key. The reserved names guard that.
    /// </para>
    /// </summary>
    private static readonly string[] PromotedAliases =
    [
        CmsConstants.Fields.Slug,
        CmsConstants.Fields.Title,
        CmsConstants.Fields.Components,
        CmsConstants.Fields.DetailPageComponents,

        // Reserved: these are fixed properties on PageDto.
        "id",
        "contentType",
        "components",
        "seo",
    ];

    public PageDto Map(IPublishedContent content, string? culture, PageMapMode mode)
    {
        content.Cultures.TryGetValue(culture ?? string.Empty, out var cultureInfo);

        var isDetail = mode == PageMapMode.Detail;

        return new PageDto
        {
            Id = content.Key.ToString(),
            ContentType = content.ContentType.Alias,
            Slug = valueMapper.Text(content, CmsConstants.Fields.Slug, culture)
                   ?? cultureInfo?.UrlSegment
                   ?? string.Empty,
            Title = valueMapper.Text(content, CmsConstants.Fields.Title, culture)
                    ?? cultureInfo?.Name
                    ?? content.Name
                    ?? string.Empty,
            Properties = MapProperties(content, culture),
            Components = isDetail ? MapComponents(content, culture) : [],
            Seo = isDetail ? seoMapper.Map(content, culture) : null,
        };
    }

    /// <summary>
    /// The item's own fields, minus anything surfaced elsewhere on the DTO.
    /// <para>
    /// SEO aliases are excluded in BOTH modes: on a detail response they belong to the
    /// <c>seo</c> object, and on a listing they are dropped entirely. Including them here
    /// would duplicate the whole SEO block in raw form.
    /// </para>
    /// </summary>
    private Dictionary<string, object?> MapProperties(IPublishedContent content, string? culture)
    {
        var excluded = new HashSet<string>(PromotedAliases, StringComparer.OrdinalIgnoreCase);

        foreach (var alias in CmsConstants.SeoFields.All)
        {
            excluded.Add(alias);
        }

        var result = new Dictionary<string, object?>();

        foreach (var property in content.Properties)
        {
            if (excluded.Contains(property.Alias)) continue;

            try
            {
                var raw = property.GetValue(culture) ?? property.GetValue(null);

                // Nested blocks and pickers still need the component mapper's recursion.
                result[property.Alias] = valueMapper.TryMapLeaf(raw, culture, out var leaf)
                    ? leaf
                    : componentMapper.MapComponents(property, culture);
            }
            catch (Exception ex)
            {
                logger.Warning(
                    ex,
                    "Skipping property {Alias} on {ContentType} — failed to map value",
                    property.Alias, content.ContentType.Alias);
                result[property.Alias] = null;
            }
        }

        return result;
    }

    /// <summary>
    /// A page keeps its components in <c>components</c>; a detail item such as a service
    /// uses <c>detailPageComponents</c>. Whichever exists is returned.
    /// </summary>
    private IReadOnlyList<ComponentDto> MapComponents(IPublishedContent content, string? culture)
    {
        var property = content.GetProperty(CmsConstants.Fields.Components)
                       ?? content.GetProperty(CmsConstants.Fields.DetailPageComponents);

        return property is null ? [] : componentMapper.MapComponents(property, culture);
    }
}
