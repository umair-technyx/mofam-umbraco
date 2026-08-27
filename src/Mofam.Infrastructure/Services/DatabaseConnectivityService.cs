using Microsoft.Extensions.Caching.Memory;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Mofam.Infrastructure.Abstractions;

namespace Mofam.Infrastructure.Services;
public sealed class DatabaseConnectivityService(
    IUmbracoDatabaseFactory databaseFactory,
    IRuntimeState runtimeState,
    IMemoryCache cache,
    Serilog.ILogger logger) : IDatabaseConnectivityService
{
    private const string CacheKey = "Mofam:DatabaseConnectivity:Status";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    public bool CanConnect()
    {
        if (runtimeState.Level != RuntimeLevel.Run) return false;

        return cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return CheckDatabase();
        });
    }

    private bool CheckDatabase()
    {
        try
        {
            if (!databaseFactory.Configured || !databaseFactory.CanConnect) return false;

            using var database = databaseFactory.CreateDatabase();
            return database.ExecuteScalar<int>("SELECT 1") == 1;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Database connectivity check failed.");
            return false;
        }
    }
}
