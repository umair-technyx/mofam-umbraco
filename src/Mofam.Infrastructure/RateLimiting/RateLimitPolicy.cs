using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;

namespace Mofam.Infrastructure.RateLimiting;

public static class RateLimitPolicy
{
    private const string FormPath = "/api/web/form";

    // The limits of the site (token bucket, see RateLimitOptions):
    // - each form: per visitor, and for all visitors together (each form has its own buckets, all with the same rule);
    // - the public API (/api/web): per visitor; the front end server itself (API key, no visitor address) is not limited;
    // - the sign in pages: per visitor (fixed window).
    public static PartitionedRateLimiter<HttpContext> Create(RateLimitOptions options, IReadOnlyCollection<string> signInPaths)
    {
        var perVisitor = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            var path = http.Request.Path;

            if (path.StartsWithSegments(FormPath, StringComparison.OrdinalIgnoreCase))
            {
                return TokenBucket($"form:{FormName(path)}:{ClientAddress.Resolve(http).Key}",
                    options.FormTokenLimit, options.FormTokensPerPeriod, options.FormPeriodSeconds);
            }

            if (options.Enabled && path.StartsWithSegments("/api/web", StringComparison.OrdinalIgnoreCase))
            {
                var client = ClientAddress.Resolve(http);
                return client.IsOurServer
                    ? RateLimitPartition.GetNoLimiter("api:server")
                    : TokenBucket($"api:{client.Key}", options.ApiTokenLimit, options.ApiTokensPerPeriod, options.ApiPeriodSeconds);
            }

            return signInPaths.Any(signInPath => path.Equals(signInPath, StringComparison.OrdinalIgnoreCase))
                ? FixedWindow($"signin:{ClientAddress.Resolve(http).Key}", options.SignInPermitLimit, TimeSpan.FromSeconds(options.SignInWindowSeconds))
                : RateLimitPartition.GetNoLimiter("none");
        });

        // Only requests with the API key count here: the rate limit runs before the key check, so a caller without
        // the key (refused with 401 anyway) must not be able to use up the total and block the form for everyone.
        var perForm = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            http.Request.Path.StartsWithSegments(FormPath, StringComparison.OrdinalIgnoreCase) && ClientAddress.HasValidKey(http)
                ? TokenBucket($"form-total:{FormName(http.Request.Path)}",
                    options.FormTotalTokenLimit, options.FormTotalTokensPerPeriod, options.FormTotalPeriodSeconds)
                : RateLimitPartition.GetNoLimiter("none"));

        return PartitionedRateLimiter.CreateChained(perVisitor, perForm);
    }

    // The last part of /api/web/form/{culture}/{form}. An unknown name shares the "other" bucket, so random paths cannot create endless buckets.
    private static string FormName(PathString path)
    {
        var name = path.Value!.TrimEnd('/').Split('/').Last().ToLowerInvariant();
        return GeneralConstants.FormNames.Contains(name) ? name : "other";
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
        });

    private static RateLimitPartition<string> TokenBucket(string key, int tokenLimit, int tokensPerPeriod, int periodSeconds) =>
        RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            TokensPerPeriod = tokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(periodSeconds),
            AutoReplenishment = true,
            QueueLimit = 0,
        });
}
