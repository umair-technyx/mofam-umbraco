using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Serilog;

namespace Mofam.Application.Notifications;

/// <summary>
/// Normalises the slug and enforces uniqueness per content type, at save time.
/// <para>
/// The API resolves detail pages by the editor-controlled <c>slug</c> property, and
/// Umbraco enforces nothing on a custom text field. Without this, two services can share
/// a slug and the API silently serves whichever it finds first.
/// </para>
/// <para>
/// Uses <see cref="ContentSavingNotification"/> rather than a *Saved* notification because
/// only the "-ing" notifications can be cancelled, which is what shows the editor an error
/// instead of letting a bad value through.
/// </para>
/// </summary>
public sealed class SlugNotificationHandler(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IShortStringHelper shortStringHelper,
    ILogger logger) : INotificationHandler<ContentSavingNotification>
{
    public void Handle(ContentSavingNotification notification)
    {
        foreach (var entity in notification.SavedEntities)
        {
            if (!entity.HasProperty(CmsConstants.Fields.Slug)) continue;

            var contentType = contentTypeService.Get(entity.ContentType.Alias);
            if (contentType is null) continue;

            // Variance is a property-level setting, not a document-level one: a doc type
            // can vary by culture while an individual property stays shared. SetValue
            // validates against the property, so asking the doc type throws
            // NotSupportedException on a shared slug under a variant doc type.
            var slugPropertyType = contentType.CompositionPropertyTypes
                .FirstOrDefault(pt => pt.Alias == CmsConstants.Fields.Slug);
            if (slugPropertyType is null) continue;

            var cultures = slugPropertyType.VariesByCulture()
                ? entity.AvailableCultures.ToArray()
                : [null!];

            foreach (var culture in cultures)
            {
                if (!Apply(entity, contentType, culture, notification)) return;
            }
        }
    }

    /// <summary>Returns false when the save was cancelled.</summary>
    private bool Apply(
        IContent entity,
        IContentType contentType,
        string? culture,
        ContentSavingNotification notification)
    {
        var raw = entity.GetValue<string>(CmsConstants.Fields.Slug, culture);

        // Empty slug: fall back to the node name so editors are not forced to type it
        // twice. Only the API depends on this value, so a sensible default is safer
        // than blocking the save.
        var source = string.IsNullOrWhiteSpace(raw)
            ? entity.GetCultureName(culture) ?? entity.Name
            : raw;

        var slug = CommonHelper.NormaliseSlug(source, shortStringHelper);

        if (string.IsNullOrEmpty(slug))
        {
            Reject(notification, culture,
                "A slug is required and could not be derived from the name.");
            return false;
        }

        var owner = FindConflict(entity, contentType.Id, slug, culture);
        if (owner is not null)
        {
            Reject(notification, culture,
                $"The slug '{slug}' is already used by '{owner.Name}'. Slugs must be unique per content type.");
            return false;
        }

        // Write back only when normalisation changed something, so untouched content
        // is not marked dirty on every save.
        if (!string.Equals(raw, slug, StringComparison.Ordinal))
        {
            entity.SetValue(CmsConstants.Fields.Slug, slug, culture);

            logger.Information(
                "Normalised slug on {ContentType} '{Name}' ({Culture}): '{From}' -> '{To}'",
                contentType.Alias, entity.Name, culture ?? "invariant", raw, slug);
        }

        return true;
    }

    /// <summary>
    /// Returns the item already holding this slug, or null when it is free. Compares
    /// against the same content type only — a page and a service may share a slug
    /// because they are reached through different URLs.
    /// </summary>
    private IContent? FindConflict(IContent entity, int contentTypeId, string slug, string? culture)
    {
        const int pageSize = 200;
        var page = 0;
        long total;

        do
        {
            var batch = contentService
                .GetPagedOfType(contentTypeId, page, pageSize, out total, null!)
                .ToList();

            foreach (var other in batch)
            {
                // Same node being re-saved is not a conflict.
                if (other.Id == entity.Id || other.Key == entity.Key) continue;
                if (other.Trashed) continue;

                var otherSlug = CommonHelper.NormaliseSlug(
                    other.GetValue<string>(CmsConstants.Fields.Slug, culture), shortStringHelper);

                if (!string.IsNullOrEmpty(otherSlug) &&
                    string.Equals(otherSlug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    return other;
                }
            }

            page++;
        }
        while (page * pageSize < total);

        return null;
    }

    private void Reject(ContentSavingNotification notification, string? culture, string message)
    {
        var scope = culture is null ? string.Empty : $" ({culture})";

        notification.Cancel = true;
        notification.Messages.Add(new EventMessage(
            $"Slug{scope}",
            message,
            EventMessageType.Error));
    }
}
