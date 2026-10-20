using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Mofam.Domain.Constants;
using Umbraco.Cms.Api.Management.Security;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Mofam.CMS.Security;

// When a user who signed in with Microsoft signs out of the backoffice, also sign out of Microsoft.
// Otherwise the next person on the same PC clicks "Microsoft" and is signed in as the previous user.
public sealed class EntraIdLogoutHandler(
    IHttpContextAccessor httpContextAccessor,
    IDataProtectionProvider dataProtectionProvider,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    IConfiguration configuration,
    ILogger<EntraIdLogoutHandler> logger) : INotificationAsyncHandler<UserLogoutSuccessNotification>
{
    public async Task HandleAsync(UserLogoutSuccessNotification notification, CancellationToken cancellationToken)
    {
        var http = httpContextAccessor.HttpContext;

        // Only for a Microsoft login. A local (username and password) user signs out as usual.
        if (http is null || !http.Request.Cookies.TryGetValue(GeneralConstants.EntraLoginCookieName, out var cookieValue)) return;

        http.Response.Cookies.Delete(
            GeneralConstants.EntraLoginCookieName,
            new CookieOptions { Path = GeneralConstants.EntraLoginCookiePath, Secure = true, HttpOnly = true });

        try
        {
            var scheme = BackOfficeAuthenticationBuilder.SchemeForBackOffice(EntraIdBackOfficeLoginOptions.SchemeName)!;
            var options = oidcOptions.Get(scheme);

            // The Microsoft sign out address comes from the tenant metadata.
            var metadata = await options.ConfigurationManager!.GetConfigurationAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(metadata.EndSessionEndpoint)) return;

            // Microsoft sends the user back here after sign out. It must be a redirect URI in Azure.
            // The configured application url is used if set, so a request header cannot change it.
            // Spaces around the value (easy to add by mistake in Key Vault) are removed.
            var applicationUrl = configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]?.Trim();
            var origin = !string.IsNullOrWhiteSpace(applicationUrl)
                ? applicationUrl.TrimEnd('/')
                : $"{http.Request.Scheme}://{http.Request.Host}";

            var query = new Dictionary<string, string?>
            {
                ["post_logout_redirect_uri"] = origin + options.SignedOutCallbackPath,
            };

            // With the logout hint, Microsoft does not ask which account to sign out.
            // No hint, or it cannot be read: the user picks the account.
            var logoutHint = ReadLogoutHint(cookieValue);
            if (logoutHint is not null)
            {
                query["logout_hint"] = logoutHint;
            }

            notification.SignOutRedirectUrl = QueryHelpers.AddQueryString(metadata.EndSessionEndpoint, query);
        }
        catch (Exception ex)
        {
            // If Microsoft cannot be reached, the user is still signed out of Umbraco.
            logger.LogWarning(ex, "Could not build the Microsoft sign out address. The Microsoft session was not ended.");
        }
    }

    // The cookie holds "1" (no hint) or the encrypted login hint.
    private string? ReadLogoutHint(string? cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue) || cookieValue == "1") return null;

        try
        {
            return dataProtectionProvider.CreateProtector(GeneralConstants.EntraLoginCookiePurpose).Unprotect(cookieValue);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
