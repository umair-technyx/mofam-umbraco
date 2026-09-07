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

    /// <summary>
    /// Single source of truth for turning content into a <see cref="PageDto"/> — including
    /// content reached through a picker anywhere inside another page. Detail, search, and
    /// every picker property all go through here, so none of those responses can drift
    /// apart, and a picked page never comes back as anything other than a listing entry.
    /// </summary>
    public PageDto Map(IPublishedContent content, string? culture, PageMapMode mode) =>
        MapInternal(content, culture, mode, new HashSet<Guid> { content.Key });

    /// <summary>
    /// Does the actual build. <paramref name="ancestors"/> is one continuous set threaded
    /// through the whole call tree — own fields AND components, and every hop through
    /// <see cref="ResolvePage"/> into a picked page — so a real cycle is caught wherever it
    /// occurs, while a node that legitimately repeats under two siblings (not an ancestor
    /// of itself) is still mapped in full both times.
    /// </summary>
    private PageDto MapInternal(IPublishedContent content, string? culture, PageMapMode mode, HashSet<Guid> ancestors)
    {
        content.Cultures.TryGetValue(culture ?? string.Empty, out var cultureInfo);

        var isDetail = mode == PageMapMode.Detail;

        // Closes over `culture`; IComponentMapper only sees a content node and the current
        // ancestor branch, never PageMapMode or PageDto — see IComponentMapper's remarks.
        PageDto ResolveForComponentMapper(IPublishedContent picked, ISet<Guid> branchAncestors) =>
            ResolvePage(picked, culture, branchAncestors);

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
            Properties = MapProperties(content, culture, ResolveForComponentMapper, ancestors),
            Components = isDetail ? MapComponents(content, culture, ResolveForComponentMapper, ancestors) : [],
            Seo = isDetail ? seoMapper.Map(content, culture) : null,
        };
    }

    /// <summary>
    /// The callback handed to <see cref="IComponentMapper"/> for every content-picker
    /// property, wherever it is found — a top-level own field (e.g. <c>gridItems</c>,
    /// <c>categories</c>) or a picker nested inside an authored block. A real content page
    /// is always mapped in <see cref="PageMapMode.Listing"/> shape here: no
    /// <c>detailPageComponents</c>, no SEO — which is also what breaks the cycle in
    /// practice for a category/service back-reference, since Listing mode never re-enters
    /// the one property (<c>detailPageComponents</c>) that could lead back out again.
    /// </summary>
    /// <remarks>
    /// This is a plain recursive call on the same <c>PageMapper</c> instance, not a second
    /// DI-resolved service — <c>ComponentMapper</c> must not depend on <c>IPageMapper</c>
    /// directly, since <c>PageMapper</c> already depends on <c>IComponentMapper</c> for the
    /// ordered components list, and the reverse edge would be a constructor-injection cycle.
    /// </remarks>
    private PageDto ResolvePage(IPublishedContent content, string? culture, ISet<Guid> ancestors)
    {
        if (ancestors.Contains(content.Key))
        {
            logger.Warning(
                "Circular page reference at {ContentType} ({Key}) — returning a minimal reference instead of recursing",
                content.ContentType.Alias, content.Key);

            return Stub(content, culture);
        }

        var branch = new HashSet<Guid>(ancestors) { content.Key };
        return MapInternal(content, culture, PageMapMode.Listing, branch);
    }

    /// <summary>
    /// A minimal, honest reference for a node already on its own ancestor branch — id,
    /// slug, title and content type only, so a client can still render a link or label
    /// instead of the loop silently disappearing into an empty object.
    /// </summary>
    private PageDto Stub(IPublishedContent content, string? culture)
    {
        content.Cultures.TryGetValue(culture ?? string.Empty, out var cultureInfo);

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
    private Dictionary<string, object?> MapProperties(
        IPublishedContent content,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
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

                // Nested blocks and pickers still need the component mapper's recursion —
                // real pages it meets come back through resolvePage above, in listing shape.
                result[property.Alias] = valueMapper.TryMapLeaf(raw, culture, out var leaf)
                    ? leaf
                    : componentMapper.MapComponents(property, culture, resolvePage, ancestors);
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
    private IReadOnlyList<ComponentDto> MapComponents(
        IPublishedContent content,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors)
    {
        var property = content.GetProperty(CmsConstants.Fields.Components)
                       ?? content.GetProperty(CmsConstants.Fields.DetailPageComponents);

        return property is null ? [] : componentMapper.MapComponents(property, culture, resolvePage, ancestors);
    }
}
