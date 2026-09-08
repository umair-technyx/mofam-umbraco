using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Domain.Options;

namespace Mofam.Application.Services;

public sealed class CachePolicy(IMemoryCache cache, IOptions<CacheOptions> options) : ICachePolicy
{
    public T? GetOrCreate<T>(string key, TimeSpan duration, Func<T?> factory)
    {
        if (!ShouldCache(duration)) return factory();

        if (cache.TryGetValue(key, out T? cached) && cached is not null) return cached;

        var value = factory();

        // Misses are not cached: a 404 is usually transient (unpublished, wrong culture)
        // and caching it would keep serving the miss after the content appears.
        if (value is not null) cache.Set(key, value, duration);

        return value;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, TimeSpan duration, Func<Task<T?>> factory)
    {
        if (!ShouldCache(duration)) return await factory();

        if (cache.TryGetValue(key, out T? cached) && cached is not null) return cached;

        var value = await factory();

        if (value is not null) cache.Set(key, value, duration);

        return value;
    }

    public void Remove(string key) => cache.Remove(key);

    private bool ShouldCache(TimeSpan duration) => options.Value.Enabled && duration > TimeSpan.Zero;
}
