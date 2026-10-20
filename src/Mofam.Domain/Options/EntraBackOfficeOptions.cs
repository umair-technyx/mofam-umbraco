namespace Mofam.Domain.Options;

public sealed class EntraBackOfficeOptions
{
    public const string SectionName = "BackOfficeEntraId";

    // Everything is off until this is true. Local login keeps working while it is false.
    public bool Enabled { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    // An empty value would break every request, so for the four settings below the default is used when the value is empty.
    private const string DefaultAuthority = "https://login.microsoftonline.com/{TenantId}/v2.0";
    private const string DefaultCallbackPath = "/signin-oidc-azure-ad";
    private const string DefaultSignedOutCallbackPath = "/signout-callback-oidc-azure-ad";
    private const string DefaultResponseMode = "query";

    private string _authority = DefaultAuthority;
    private string _callbackPath = DefaultCallbackPath;
    private string _signedOutCallbackPath = DefaultSignedOutCallbackPath;
    private string _responseMode = DefaultResponseMode;

    // The Azure login address. {TenantId} is replaced with the TenantId above.
    public string Authority
    {
        get => _authority;
        set => _authority = string.IsNullOrWhiteSpace(value) ? DefaultAuthority : value;
    }

    // Must be a redirect URI registered in Azure.
    public string CallbackPath
    {
        get => _callbackPath;
        set => _callbackPath = string.IsNullOrWhiteSpace(value) ? DefaultCallbackPath : value;
    }

    // The address Microsoft sends the user to after sign out.
    public string SignedOutCallbackPath
    {
        get => _signedOutCallbackPath;
        set => _signedOutCallbackPath = string.IsNullOrWhiteSpace(value) ? DefaultSignedOutCallbackPath : value;
    }

    // "query": the result comes back in the address (GET). "form_post": it comes as a form (POST).
    public string ResponseMode
    {
        get => _responseMode;
        set => _responseMode = string.IsNullOrWhiteSpace(value) ? DefaultResponseMode : value;
    }

    // When true, username/password login is removed. Turn on only after Entra login is tested.
    public bool DenyLocalLogin { get; set; }

    // When true, a user can link or unlink the Microsoft account in their profile.
    // Keep false: the link is made only automatically at the first login.
    public bool AllowManualLinking { get; set; }

    // When true, the login page goes straight to Microsoft (no button).
    // Turn on only after Entra login is tested.
    public bool AutoRedirect { get; set; }

    // Extra Umbraco groups for an Azure role. The key is the role value.
    // Example: "admin": ["sensitiveData"] gives the admin role the sensitive data group too.
    public Dictionary<string, string[]> ExtraGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
