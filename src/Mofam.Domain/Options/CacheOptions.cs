namespace Mofam.Domain.Options;

/// <summary>
/// Caching belongs in config because it genuinely varies per environment: off in
/// development so editors see changes immediately, on with longer windows in production.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Master switch. When false, no API response is cached at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Site root lookup. Only the node key is cached and content is re-resolved each
    /// request, so this can be generous.
    /// </summary>
    public int SiteRootSeconds { get; set; } = 300;

    /// <summary>Filter options. Stale until this expires, so keep it modest until publish-based eviction exists.</summary>
    public int FilterSeconds { get; set; } = 300;

    /// <summary>Page responses. The heaviest payload and the most visible if stale.</summary>
    public int PageSeconds { get; set; } = 60;

    /// <summary>
    /// How long a page that was not found is remembered, so a repeated wrong slug does not search the index each time.
    /// It is cleared when content is published. A page asked for in the short moment between the publish and the
    /// index update can still show as not found until this time has passed. 0 turns it off.
    /// </summary>
    public int PageNotFoundSeconds { get; set; } = 15;

    /// <summary>Startup payload — header, footer and dictionary.</summary>
    public int StartupSeconds { get; set; } = 300;

    /// <summary>The Contact Us admin email and templates. Also cleared when content is published.</summary>
    public int ContactConfigSeconds { get; set; } = 300;

    public TimeSpan SiteRoot => TimeSpan.FromSeconds(SiteRootSeconds);
    public TimeSpan Filters => TimeSpan.FromSeconds(FilterSeconds);
    public TimeSpan Page => TimeSpan.FromSeconds(PageSeconds);
    public TimeSpan PageNotFound => TimeSpan.FromSeconds(PageNotFoundSeconds);
    public TimeSpan Startup => TimeSpan.FromSeconds(StartupSeconds);
    public TimeSpan ContactConfig => TimeSpan.FromSeconds(ContactConfigSeconds);
}
