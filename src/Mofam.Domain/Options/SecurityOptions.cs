namespace Mofam.Domain.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string ApiKey { get; set; } = string.Empty;
}
