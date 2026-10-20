namespace Mofam.Domain.Constants;
public static class GeneralConstants
{
    #region Request size

    // The most bytes a request body to the API (/api/...) may have. A bigger one is refused before it is read.
    public const int MaxApiRequestBodyBytes = 1024 * 1024;

    #endregion

    #region Email and phone patterns

    // One plain email address (name@domain.tld): no display text, no comments, no second address and no line breaks.
    // At most 254 characters, and at most 64 before the @.
    public const string EmailPattern =
        @"^(?=.{1,254}\z)(?!\.)(?!.*\.\.)[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]{1,64}(?<!\.)@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+\z";

    // An optional + at the start, then digits (Arabic digits too), spaces, dots, hyphens and brackets. 7 to 15 digits (the international limit).
    public const string PhonePattern =
        @"^\+?(?=(?:\D*\d){7,15}\D*\z)[\d ().\-]+\z";

    #endregion

    #region Forms enums
    public enum RecaptchaStatus
    {
        // reCAPTCHA is turned off in the settings.
        Skipped,

        // The token is valid, and the score is high enough.
        Passed,

        // The token is missing, wrong, for another action, or the score is too low.
        Failed,

        // Google could not be reached, or reCAPTCHA is turned on but not set up.
        Unavailable,
    }

    #endregion

    #region Failure messages

    // The generic messages for a failure (503 and 500). They never show what went wrong inside the server.
    public const string ServiceUnavailableMessage = "The service is temporarily unavailable. Please try again later.";
    public const string UnexpectedErrorMessage = "Something went wrong. Please try again later.";

    #endregion

    #region Contact Us field messages

    // For one field. They are the ErrorMessage of the attributes on ContactRequest: {0} is the field name, {1} is the number.
    public const string FieldRequiredMessage = "{0} is required.";
    public const string FieldTooLongMessage = "{0} must be at most {1} characters.";
    public const string FieldInvalidMessage = "{0} is not valid.";
    public const string FieldInvalidCharactersMessage = "{0} has characters that are not allowed.";

    #endregion

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

    #region Forms

    // The forms that have their own rate limit counter (the last part of the path /api/web/form/{culture}/{form}).
    // Add the name of each new form here. Any other name shares one "other" counter, so random paths cannot create endless counters.
    public static readonly string[] FormNames = ["contact"];

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
