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

// Cleans the slug and makes it unique for each content type, when the editor saves.
// The API finds detail pages by the slug property, and Umbraco does not check it, so two nodes could share a slug.
// This uses the "Saving" notification because only it can be cancelled, which shows the editor an error.
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

            // Whether a value varies by culture is set on the property, not on the document type,
            // so the slug property is checked here.
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

    // Returns false when the save was cancelled.
    private bool Apply(
        IContent entity,
        IContentType contentType,
        string? culture,
        ContentSavingNotification notification)
    {
        var raw = entity.GetValue<string>(CmsConstants.Fields.Slug, culture);

        // An empty slug is made from the node name, so the editor does not have to type it twice.
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

        // The value is written back only when it changed, so the content is not marked as edited on every save.
        if (!string.Equals(raw, slug, StringComparison.Ordinal))
        {
            entity.SetValue(CmsConstants.Fields.Slug, slug, culture);

            logger.Information(
                "Normalised slug on {ContentType} '{Name}' ({Culture}): '{From}' -> '{To}'",
                contentType.Alias, entity.Name, culture ?? "invariant", raw, slug);
        }

        return true;
    }

    // Gives the node that already has this slug, or null. Only the same content type is checked,
    // because a page and a service may share a slug (their URLs are different).
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
                // The same node being saved again is not a conflict.
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
