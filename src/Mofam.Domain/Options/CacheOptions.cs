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

    /// <summary>Startup payload — header, footer and dictionary.</summary>
    public int StartupSeconds { get; set; } = 300;

    public TimeSpan SiteRoot => TimeSpan.FromSeconds(SiteRootSeconds);
    public TimeSpan Filters => TimeSpan.FromSeconds(FilterSeconds);
    public TimeSpan Page => TimeSpan.FromSeconds(PageSeconds);
    public TimeSpan Startup => TimeSpan.FromSeconds(StartupSeconds);
}
