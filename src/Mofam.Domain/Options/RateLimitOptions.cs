namespace Mofam.Domain.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}
