namespace Mofam.Application.Abstractions;

/// <summary>
/// One place that knows whether caching is on and for how long, so services never
/// branch on it themselves. With caching disabled the factory simply runs every time
/// and the cache is not touched.
/// </summary>
public interface ICachePolicy
{
    T? GetOrCreate<T>(string key, TimeSpan duration, Func<T?> factory);

    Task<T?> GetOrCreateAsync<T>(string key, TimeSpan duration, Func<Task<T?>> factory);

    /// <summary>Drops a single entry — used when a cached key turns out to be stale.</summary>
    void Remove(string key);
}
