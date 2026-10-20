namespace Mofam.Domain.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    // Turns the limit of the public API (/api/web/...) on or off. The sign in and form limits stay on.
    public bool Enabled { get; set; } = true;

    // The public API: each client address may call it this many times in this many seconds.
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }

    // The sign in callback, the sign out callback and the sign in error page are open to everyone,
    // so each client address may call them only this many times in this many seconds.
    public int SignInPermitLimit { get; set; }
    public int SignInWindowSeconds { get; set; }

    // The forms (/api/web/form/...): each form has its own counter, and each client address may submit it this many times in this many seconds.
    public int FormPermitLimit { get; set; } = 5;
    public int FormWindowSeconds { get; set; } = 600;
}
