using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Domain.Options;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Services;

public sealed class SiteRootResolver(
    IPublishedContentQuery contentQuery,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    ILogger logger) : ISiteRootResolver
{
    private const string CacheKeyPrefix = "mofam:site-root:";

    public IPublishedContent? GetRoot(string rootAlias)
    {
        if (string.IsNullOrWhiteSpace(rootAlias)) return null;

        var cacheKey = CacheKeyPrefix + rootAlias;

        // Only the key is cached, never the IPublishedContent itself — published content
        // instances belong to a request-scoped snapshot and must not outlive it.
        var rootKey = cachePolicy.GetOrCreate<Guid?>(
            cacheKey,
            cacheOptions.Value.SiteRoot,
            () => FindRoot(rootAlias)?.Key);

        if (rootKey is null)
        {
            logger.Warning("No site root found for alias {RootAlias}", rootAlias);
            return null;
        }

        var root = contentQuery.Content(rootKey.Value);
        if (root is not null) return root;

        // Cached key no longer resolves — node moved, deleted or unpublished. Drop it
        // and go back to the tree once.
        cachePolicy.Remove(cacheKey);
        return FindRoot(rootAlias);
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
