using Microsoft.Extensions.Caching.Memory;
using Mofam.Application.Services;
using Mofam.Domain.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Mofam.Application.Notifications;
public sealed class ContentCacheNotificationHandler(IMemoryCache cache, PageCacheToken pageCacheToken): INotificationHandler<ContentCacheRefresherNotification>
{
    public void Handle(ContentCacheRefresherNotification notification)
    {
        // Pages: one reset clears every cached page, whatever its slug.
        pageCacheToken.Reset();

        foreach (var culture in CmsConstants.Cultures.All)
        {
            // Startup
            cache.Remove($"{StartupService.CacheKeyPrefix}{culture}");

            // Filters (the key uses the content type in lower case, see FilterService)
            foreach (var contentType in SearchConstants.FilterDefinitions.Keys)
            {
                cache.Remove($"{FilterService.CacheKeyPrefix}{contentType.ToLowerInvariant()}:{culture}");
            }
        }
    }
}
