namespace Mofam.Domain.Options;

public sealed class ReCaptchaOptions
{
    public const string SectionName = "GoogleReCaptcha";
    public bool Enabled { get; set; } = true;
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public double MinimumScore { get; set; } = 0.5;
    public string[] AllowedHostnames { get; set; } = [];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
