namespace Mofam.Domain.Options;

public sealed class ReCaptchaOptions
{
    public const string SectionName = "GoogleReCaptcha";

    public bool Enabled { get; set; } = true;

    // The reCAPTCHA Enterprise site key. The front end uses it; the backend does not send it to Google.
    public string SiteKey { get; set; } = string.Empty;

    // The reCAPTCHA Enterprise legacy secret key, sent to the siteverify endpoint.
    public string SecretKey { get; set; } = string.Empty;

    // A score below this value fails the check.
    public double MinimumScore { get; set; } = 0.5;

    // Hostnames a token may come from (the front end hosts, for example "mofam-fe.example.com").
    // Google reports the hostname of the page that made the token. A token from any other host fails.
    // Empty = not checked here (then the allowed domains of the key in the Google console are the only limit).
    public string[] AllowedHostnames { get; set; } = [];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SiteKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}
