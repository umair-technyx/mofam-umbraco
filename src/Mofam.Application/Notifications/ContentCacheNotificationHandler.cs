using Mofam.Application.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Mofam.Application.Notifications;

// Clears the API cache when content, a dictionary item (a translation) or a media item changes in the CMS.
// Pages, startup and filters are all linked to the publish token, so one reset clears them together.
public sealed class ContentCacheNotificationHandler(PageCacheToken pageCacheToken) :
    INotificationHandler<ContentCacheRefresherNotification>,
    INotificationHandler<DictionaryCacheRefresherNotification>,
    INotificationHandler<MediaCacheRefresherNotification>
{
    public void Handle(ContentCacheRefresherNotification notification) => pageCacheToken.Reset();

    // Labels and texts in the startup response come from the dictionary.
    public void Handle(DictionaryCacheRefresherNotification notification) => pageCacheToken.Reset();

    // A replaced logo or image changes the startup response and the pages that show it.
    public void Handle(MediaCacheRefresherNotification notification) => pageCacheToken.Reset();
}
