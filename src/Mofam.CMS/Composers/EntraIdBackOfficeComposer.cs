using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Mofam.CMS.Security;
using Mofam.Infrastructure.Services;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;
using Umbraco.Cms.Api.Management.Security;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Manifest;
using Umbraco.Extensions;

namespace Mofam.CMS.Composers;

// Adds the "Sign in with Microsoft" button to the Umbraco backoffice login.
// Nothing is added unless BackOfficeEntraId:Enabled is true.
public sealed class EntraIdBackOfficeComposer : IComposer
{
    // Cleans text from outside before it goes to the log (removes line breaks), so nobody can write fake log lines.
    private static string? SafeLogText(string? text)
    {
        if (text is null) return null;

        var clean = new string(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray());

        return clean.Length > GeneralConstants.LogTextMaxLength ? clean[..GeneralConstants.LogTextMaxLength] : clean;
    }

    public void Compose(IUmbracoBuilder builder)
    {
        var section = builder.Config.GetSection(EntraBackOfficeOptions.SectionName);
        var settings = section.Get<EntraBackOfficeOptions>();

        if (settings is not { Enabled: true }) return;

        // If a setting is empty, the site must still start. The error is written to the log and the Microsoft login will not work.
        var missing = new[]
            {
                (Name: nameof(settings.TenantId), settings.TenantId),
                (Name: nameof(settings.ClientId), settings.ClientId),
                (Name: nameof(settings.ClientSecret), settings.ClientSecret),
            }
            .Where(x => string.IsNullOrWhiteSpace(x.Item2))
            .Select(x => x.Name)
            .ToList();

        if (missing.Count > 0)
        {
            LoggingService.AppLogger.Error(
                "BackOfficeEntraId is enabled but these settings are empty: {Missing}. The Microsoft login will not work until they are set.",
                string.Join(", ", missing));
        }

        builder.Services.Configure<EntraBackOfficeOptions>(section);
        builder.Services.ConfigureOptions<EntraIdBackOfficeLoginOptions>();

        // At sign out, also sign out of Microsoft.
        builder.AddNotificationAsyncHandler<UserLogoutSuccessNotification, EntraIdLogoutHandler>();

        // The "Sign in with Microsoft" button.
        builder.Services.AddSingleton<IPackageManifestReader, EntraIdPackageManifestReader>();

        // The scheme name must start with "Umbraco." for the backoffice.
        var scheme = BackOfficeAuthenticationBuilder.SchemeForBackOffice(EntraIdBackOfficeLoginOptions.SchemeName)!;

        builder.AddBackOfficeExternalLogins(logins =>
            logins.AddBackOfficeLogin(
                backOffice => backOffice.AddOpenIdConnect(scheme, options =>
                {
                    // Spaces around the values (easy to add by mistake in Key Vault) are removed where they are used.
                    // The tenant address. {TenantId} is replaced with the tenant id.
                    options.Authority = settings.Authority.Trim().Replace("{TenantId}", settings.TenantId.Trim());
                    // An empty ClientId breaks every request, so a placeholder is used. The error is in the log.
                    options.ClientId = string.IsNullOrWhiteSpace(settings.ClientId) ? "not-configured" : settings.ClientId.Trim();
                    options.ClientSecret = settings.ClientSecret.Trim();

                    // Must match a redirect URI in Azure.
                    options.CallbackPath = settings.CallbackPath.Trim();
                    options.SignedOutCallbackPath = settings.SignedOutCallbackPath.Trim();

                    // Always the code flow with PKCE.
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.ResponseMode = settings.ResponseMode.Trim();
                    options.UsePkce = true;
                    options.MapInboundClaims = false;
                    // Tokens are not saved, we only need to know who the user is.
                    options.SaveTokens = false;

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");

                    // Umbraco uses the name claim as the user name.
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.TokenValidationParameters.RoleClaimType = "roles";

                    options.Events = new OpenIdConnectEvents
                    {
                        // After Microsoft sign out, go to the backoffice login page.
                        OnSignedOutCallbackRedirect = context =>
                        {
                            context.Response.Redirect(GeneralConstants.BackOfficePath);
                            context.HandleResponse();
                            return Task.CompletedTask;
                        },

                        // If a setting is empty the login cannot work, so log the reason and show the error page.
                        OnRedirectToIdentityProvider = context =>
                        {
                            if (missing.Count > 0)
                            {
                                var reference = context.HttpContext.TraceIdentifier;

                                context.HttpContext.RequestServices.GetRequiredService<ILogger<EntraIdBackOfficeComposer>>()
                                    .LogError("Microsoft (Entra) login cannot start because these settings are empty: {Missing}. Reference {Reference}.",
                                        string.Join(", ", missing), reference);

                                context.Response.Redirect(context.HttpContext.RequestServices.GetRequiredService<SignInErrorToken>().UrlFor(GeneralConstants.NotConfigured, reference));
                                context.HandleResponse();
                            }

                            return Task.CompletedTask;
                        },

                        // The Microsoft login failed. Log the reason and show the error page.
                        OnRemoteFailure = context =>
                        {
                            var reference = context.HttpContext.TraceIdentifier;

                            context.HttpContext.RequestServices.GetRequiredService<ILogger<EntraIdBackOfficeComposer>>()
                                .LogWarning(context.Failure, "Microsoft (Entra) login failed: {Reason}. Reference {Reference}.",
                                    SafeLogText(context.Failure?.Message), reference);

                            var reason = context.Failure is EntraNoRoleException
                                ? GeneralConstants.NoRole
                                : GeneralConstants.Failed;

                            context.Response.Redirect(context.HttpContext.RequestServices.GetRequiredService<SignInErrorToken>().UrlFor(reason, reference));
                            context.HandleResponse();
                            return Task.CompletedTask;
                        },

                        // The user cancelled, or Microsoft did not allow the sign in.
                        OnAccessDenied = context =>
                        {
                            var reference = context.HttpContext.TraceIdentifier;

                            context.HttpContext.RequestServices.GetRequiredService<ILogger<EntraIdBackOfficeComposer>>()
                                .LogWarning("Microsoft (Entra) login was denied or cancelled. Reference {Reference}.", reference);

                            context.Response.Redirect(context.HttpContext.RequestServices.GetRequiredService<SignInErrorToken>().UrlFor(GeneralConstants.Denied, reference));
                            context.HandleResponse();
                            return Task.CompletedTask;
                        },

                        // Umbraco needs an email claim. Entra often sends it as preferred_username.
                        OnTokenValidated = async context =>
                        {
                            // No user in the token, or no Azure role that matches an Umbraco group, means no login.
                            // This runs before Umbraco creates the user.
                            if (context.Principal is null)
                            {
                                context.Fail(new InvalidOperationException("The Microsoft login did not return a user."));
                                return;
                            }

                            var services = context.HttpContext.RequestServices;

                            var existingAliases = (await services.GetRequiredService<IUserGroupService>().GetAllAsync(0, int.MaxValue))
                                .Items
                                .Select(g => g.Alias)
                                .ToList();

                            var groups = EntraGroupMapper.Resolve(EntraGroupMapper.RolesFrom(context.Principal), existingAliases, settings);

                            if (groups.Count == 0)
                            {
                                services.GetRequiredService<ILogger<EntraIdBackOfficeComposer>>().LogWarning(
                                    "Microsoft (Entra) login refused: the user (oid {Oid}) has no App Role that matches an Umbraco user group. Ask DevOps to assign a role. Reference {Reference}.",
                                    context.Principal.FindFirst("oid")?.Value, context.HttpContext.TraceIdentifier);

                                context.Fail(new EntraNoRoleException("The user has no App Role that matches an Umbraco user group."));
                                return;
                            }

                            var identity = context.Principal.Identities.FirstOrDefault();

                            // Umbraco needs the NameIdentifier claim to know the Azure user, or the login loops.
                            // "oid" is the user id in the tenant. "sub" is used only if "oid" is missing.
                            if (identity is not null && !identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                            {
                                var userId = identity.FindFirst("oid")?.Value ?? identity.FindFirst("sub")?.Value;

                                if (!string.IsNullOrWhiteSpace(userId))
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
                                }
                            }

                            if (identity is not null && !identity.HasClaim(c => c.Type == ClaimTypes.Email))
                            {
                                var email = identity.FindFirst("email")?.Value
                                            ?? identity.FindFirst("preferred_username")?.Value
                                            ?? identity.FindFirst("upn")?.Value;

                                if (!string.IsNullOrWhiteSpace(email))
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.Email, email));
                                }
                            }

                            // Remember that this login came from Microsoft, so sign out can also sign out of Microsoft.
                            // The login_hint claim (set up in Azure) is saved encrypted and sent as logout_hint, so Microsoft does not ask for the account.
                            // No claim: the cookie holds "1" and the user picks the account.
                            var loginHint = context.Principal.FindFirst("login_hint")?.Value
                                            ?? context.SecurityToken?.Claims.FirstOrDefault(c => c.Type == "login_hint")?.Value;
                            var cookieValue = "1";

                            if (!string.IsNullOrWhiteSpace(loginHint))
                            {
                                cookieValue = context.HttpContext.RequestServices
                                    .GetRequiredService<IDataProtectionProvider>()
                                    .CreateProtector(GeneralConstants.EntraLoginCookiePurpose)
                                    .Protect(loginHint);
                            }

                            context.HttpContext.Response.Cookies.Append(
                                GeneralConstants.EntraLoginCookieName,
                                cookieValue,
                                new CookieOptions
                                {
                                    HttpOnly = true,
                                    Secure = true,
                                    SameSite = SameSiteMode.Lax,
                                    Path = GeneralConstants.EntraLoginCookiePath,
                                });
                        },
                    };
                }),
                // Auto-link and groups are set in EntraIdBackOfficeLoginOptions.
                _ => { }));
    }
}
