namespace Mofam.Domain.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }

    // The sign in callback, the sign out callback and the sign in error page are open to everyone,
    // so each client address may call them only this many times in this many seconds.
    public int SignInPermitLimit { get; set; }
    public int SignInWindowSeconds { get; set; }
}
