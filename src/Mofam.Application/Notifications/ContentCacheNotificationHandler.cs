using Microsoft.Extensions.Caching.Memory;
using Mofam.Application.Services;
using Mofam.Domain.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Mofam.Application.Notifications;

// Clears the startup, filter and page cache when content is published, unpublished or deleted in the CMS,
// so the API shows the new content right away.
public sealed class ContentCacheNotificationHandler(IMemoryCache cache, PageCacheToken pageCacheToken)
    : INotificationHandler<ContentCacheRefresherNotification>
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
