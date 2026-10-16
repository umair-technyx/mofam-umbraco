namespace Mofam.Domain.Constants;

// General values used in more than one place: the client name and the sign in error page (address, reasons, messages).
public static class GeneralConstants
{
    #region Client

    // The client name shown on the status page and the sign in error page.
    public const string ClientName = "MOFAM";

    #endregion

    #region Microsoft sign out

    // A cookie set when the user signs in with Microsoft. At sign out it tells us to sign out of Microsoft too.
    // It lives until the browser is closed.
    public const string EntraLoginCookieName = "BackOfficeEntraLogin";

    // The cookie holds the Microsoft login hint (encrypted), so Microsoft does not ask which account to sign out.
    // This is the name of the key that protects it.
    public const string EntraLoginCookiePurpose = "Mofam.EntraLogin.v1";

    // The cookie is sent only to the backoffice.
    public const string EntraLoginCookiePath = "/umbraco";

    // Where the user goes after Microsoft sign out.
    public const string BackOfficePath = "/umbraco";

    #endregion

    #region Logging

    // The most characters of an outside error text written to the log.
    public const int LogTextMaxLength = 200;

    #endregion

    #region Sign in error page

    // The address of the sign in error page.
    public const string SignInErrorPath = "/sign-in-error";

    // The error page opens only with a valid token in the address (?t=...). The token is encrypted and expires.
    public const string SignInErrorTokenQuery = "t";

    // How long (in minutes) the token stays valid.
    public const int SignInErrorTokenMinutes = 5;

    // The name of the key that protects the token. If it is changed, old tokens stop working.
    public const string SignInErrorTokenPurpose = "Mofam.SignInError.v1";

    // The reasons the Microsoft sign in can fail, with the title and message for each.
    // The composer sends a reason code to the error page, and the page takes its text from here.
    // NoRole: the Microsoft account is valid, but it has no App Role for an Umbraco group.
    public const string NoRole = "no-role";

    // NotConfigured: TenantId, ClientId or ClientSecret is not set.
    public const string NotConfigured = "not-configured";

    // Denied: the user cancelled, or Microsoft did not allow the sign in.
    public const string Denied = "denied";

    // Failed: any other problem.
    public const string Failed = "failed";

    // The title and message for each reason. Keep them generic, they must not show what went wrong inside.
    private static readonly Dictionary<string, (string Title, string Message)> Texts = new()
    {
        [NoRole] =
        (
            "Access not granted",
            "Your Microsoft account was recognised, but it has not been given access to this system. " +
            "Please contact your Azure administrator and ask them to assign you a login role."
        ),
        [NotConfigured] =
        (
            "Sign in is unavailable",
            "Sign in with Microsoft is not available at the moment. Please try again later. " +
            "If the problem continues, please contact your system administrator."
        ),
        [Denied] =
        (
            "Sign in was not completed",
            "The sign in was cancelled or was not permitted. You may return to the sign in page and try again. " +
            "If you need access, please contact your Azure administrator."
        ),
        [Failed] =
        (
            "Sign in unsuccessful",
            "We were unable to complete your sign in. Please try again. " +
            "If the problem continues, please contact your system administrator."
        ),
    };

    // An unknown or missing reason gets the "Failed" text, so nothing from the address is shown.
    public static (string Title, string Message) TextFor(string? reason) =>
        Texts.TryGetValue(reason ?? string.Empty, out var text) ? text : Texts[Failed];

    #endregion
}
