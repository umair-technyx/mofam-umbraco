using Microsoft.Extensions.Caching.Memory;
using Mofam.Application.Services;
using Mofam.Domain.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Mofam.Application.Notifications;

// Clears the cached startup response when content changes in the CMS (publish, unpublish, delete).
// It listens to the cache refresh event, which fires after Umbraco has updated its own published
// cache, so the next startup request is built from the new content.
public sealed class StartupCacheNotificationHandler(IMemoryCache cache)
    : INotificationHandler<ContentCacheRefresherNotification>
{
    public void Handle(ContentCacheRefresherNotification notification)
    {
        foreach (var culture in CmsConstants.Cultures.All)
        {
            cache.Remove($"{StartupService.CacheKeyPrefix}{culture}");
        }
    }
}
