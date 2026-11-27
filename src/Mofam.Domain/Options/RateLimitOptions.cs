namespace Mofam.Domain.Options;

// Token bucket limits: a visitor has up to TokenLimit tokens, each request uses one,
// and TokensPerPeriod tokens come back every PeriodSeconds. So a short burst is fine and nobody waits long.
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    // Turns the limit of the public API (/api/web/...) on or off. The form limits stay on.
    public bool Enabled { get; set; } = true;

    // The public API, per visitor: up to 300 requests at once, 30 more every second.
    public int ApiTokenLimit { get; set; } = 300;
    public int ApiTokensPerPeriod { get; set; } = 30;
    public int ApiPeriodSeconds { get; set; } = 1;

    // The sign in callback, the sign out callback and the sign in error page are open to everyone,
    // so each client address may call them only this many times in this many seconds.
    public int SignInPermitLimit { get; set; } = 20;
    public int SignInWindowSeconds { get; set; } = 60;

    // Each form (/api/web/form/...), per visitor: up to 10 submits at once, 1 more every 6 seconds.
    public int FormTokenLimit { get; set; } = 10;
    public int FormTokensPerPeriod { get; set; } = 1;
    public int FormPeriodSeconds { get; set; } = 6;

    // Each form, all visitors together, so a bot with many addresses cannot flood it: up to 1000 at once, 20 more every second (about 1,200 a minute).
    public int FormTotalTokenLimit { get; set; } = 1000;
    public int FormTotalTokensPerPeriod { get; set; } = 20;
    public int FormTotalPeriodSeconds { get; set; } = 1;
}
