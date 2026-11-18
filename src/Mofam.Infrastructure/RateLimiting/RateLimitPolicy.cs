using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;

namespace Mofam.Infrastructure.RateLimiting;

public static class RateLimitPolicy
{
    private const string FormPath = "/api/web/form";

    // The limits of the site:
    // - each form: per visitor, and for all visitors together (each form has its own counters, all with the same rule);
    // - the public API (/api/web): per visitor; the front end server itself (API key, no visitor address) is not limited;
    // - the sign in pages: per visitor.
    public static PartitionedRateLimiter<HttpContext> Create(RateLimitOptions options, IReadOnlyCollection<string> signInPaths)
    {
        var formWindow = TimeSpan.FromSeconds(options.FormWindowSeconds);

        var perVisitor = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            var path = http.Request.Path;

            if (path.StartsWithSegments(FormPath, StringComparison.OrdinalIgnoreCase))
            {
                return FixedWindow($"form:{FormName(path)}:{ClientAddress.Resolve(http).Key}", options.FormPermitLimit, formWindow);
            }

            if (options.Enabled && path.StartsWithSegments("/api/web", StringComparison.OrdinalIgnoreCase))
            {
                var client = ClientAddress.Resolve(http);
                return client.IsOurServer
                    ? RateLimitPartition.GetNoLimiter("api:server")
                    : FixedWindow($"api:{client.Key}", options.PermitLimit, TimeSpan.FromSeconds(options.WindowSeconds));
            }

            return signInPaths.Any(signInPath => path.Equals(signInPath, StringComparison.OrdinalIgnoreCase))
                ? FixedWindow($"signin:{ClientAddress.Resolve(http).Key}", options.SignInPermitLimit, TimeSpan.FromSeconds(options.SignInWindowSeconds))
                : RateLimitPartition.GetNoLimiter("none");
        });

        // Only requests with the API key count here: the rate limit runs before the key check, so a caller without
        // the key (refused with 401 anyway) must not be able to use up the total and block the form for everyone.
        var perForm = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            http.Request.Path.StartsWithSegments(FormPath, StringComparison.OrdinalIgnoreCase) && ClientAddress.HasValidKey(http)
                ? FixedWindow($"form-total:{FormName(http.Request.Path)}", options.FormTotalPermitLimit, formWindow)
                : RateLimitPartition.GetNoLimiter("none"));

        return PartitionedRateLimiter.CreateChained(perVisitor, perForm);
    }

    // The last part of /api/web/form/{culture}/{form}. An unknown name shares the "other" counters, so random paths cannot create endless counters.
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
}
