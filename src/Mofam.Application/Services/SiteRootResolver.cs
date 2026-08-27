using Microsoft.Extensions.Caching.Memory;
using Mofam.Application.Abstractions;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Mofam.Application.Services;

public sealed class SiteRootResolver(
    IPublishedContentQuery contentQuery,
    IMemoryCache cache,
    ILogger logger) : ISiteRootResolver
{
    private const string CacheKeyPrefix = "mofam:site-root:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public IPublishedContent? GetRoot(string rootAlias)
    {
        if (string.IsNullOrWhiteSpace(rootAlias)) return null;

        var cacheKey = CacheKeyPrefix + rootAlias;

        // Only the key is cached, never the IPublishedContent itself — published content
        // instances belong to a request-scoped snapshot and must not outlive it.
        if (cache.TryGetValue(cacheKey, out Guid cachedKey))
        {
            var cached = contentQuery.Content(cachedKey);
            if (cached is not null) return cached;

            // Node was moved, deleted or unpublished since we cached it.
            cache.Remove(cacheKey);
        }

        var root = FindRoot(rootAlias);
        if (root is null)
        {
            logger.Warning("No site root found for alias {RootAlias}", rootAlias);
            return null;
        }

        cache.Set(cacheKey, root.Key, CacheDuration);
        return root;
    }

    // Channel roots normally sit one level under the content root, but tolerate them
    // being at the very top too.
    //
    // NOTE: uses the obsolete IPublishedContent.Children property. The Umbraco 17
    // replacement needs INavigationQueryService + IPublishedStatusFilteringService
    // injected; that migration is required before Umbraco 18 and is tracked separately.
#pragma warning disable CS0618
    private IPublishedContent? FindRoot(string rootAlias)
    {
        var atRoot = contentQuery.ContentAtRoot().ToList();

        return atRoot
                   .SelectMany(r => r.Children)
                   .FirstOrDefault(c => c.ContentType.Alias == rootAlias)
               ?? atRoot
                   .FirstOrDefault(c => c.ContentType.Alias == rootAlias);
    }
#pragma warning restore CS0618
}
