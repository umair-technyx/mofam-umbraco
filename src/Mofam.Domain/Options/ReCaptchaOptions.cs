namespace Mofam.Domain.Options;

public sealed class ReCaptchaOptions
{
    public const string SectionName = "GoogleReCaptcha";

    public bool Enabled { get; set; } = true;

    public string ProjectId { get; set; } = string.Empty;

    public string SiteKey { get; set; } = string.Empty;

    public string CredentialsJsonBase64 { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(SiteKey)
        && !string.IsNullOrWhiteSpace(CredentialsJsonBase64);
}
