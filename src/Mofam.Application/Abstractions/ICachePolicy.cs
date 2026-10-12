namespace Mofam.Application.Abstractions;

// One place that knows if caching is on and for how long, so services do not check it
// themselves. When caching is off, the factory runs every time and the cache is not used.
public interface ICachePolicy
{
    #region Get or create

    T? GetOrCreate<T>(string key, TimeSpan duration, Func<T?> factory);

    Task<T?> GetOrCreateAsync<T>(string key, TimeSpan duration, Func<Task<T?>> factory);

    #endregion

    #region Remove

    // Removes one entry. Used when a cached key is no longer valid.
    void Remove(string key);

    #endregion
}
