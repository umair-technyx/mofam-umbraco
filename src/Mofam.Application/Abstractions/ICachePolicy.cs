namespace Mofam.Application.Abstractions;

// One place that knows if caching is on and for how long, so services do not check it
// themselves. When caching is off, the factory runs every time and the cache is not used.
public interface ICachePolicy
{
    #region Get or create

    // If expireWith is given, the entry also expires as soon as that token is cancelled.
    // Pass the token before running the factory, so a reset during the build is not missed.
    T? GetOrCreate<T>(string key, TimeSpan duration, Func<T?> factory, CancellationToken expireWith = default);

    Task<T?> GetOrCreateAsync<T>(string key, TimeSpan duration, Func<Task<T?>> factory);

    #endregion

    #region Remembered misses

    // Reads an entry without creating it. False when caching is off or the entry is not there.
    bool TryGet<T>(string key, out T? value);

    // Stores an entry for the given time. Does nothing when caching is off or the time is not above zero.
    // If expireWith is given, the entry also expires as soon as that token is cancelled.
    void Set<T>(string key, T value, TimeSpan duration, CancellationToken expireWith = default);

    #endregion

    #region Remove

    // Removes one entry. Used when a cached key is no longer valid.
    void Remove(string key);

    #endregion
}
