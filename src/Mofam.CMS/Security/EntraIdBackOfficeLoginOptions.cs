using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Mofam.Domain.Options;
using Umbraco.Cms.Api.Management.Security;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Mofam.CMS.Security;

// Settings for the Entra ID login in the Umbraco backoffice.
// A new Azure user is created in Umbraco at the first login. The groups come from the Azure App Roles
// and are checked and saved again on every login.
public sealed class EntraIdBackOfficeLoginOptions(
    IOptions<EntraBackOfficeOptions> entraOptions,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EntraIdBackOfficeLoginOptions> logger) : IConfigureNamedOptions<BackOfficeExternalLoginProviderOptions>
{
    // The name of the login provider. Umbraco adds "Umbraco." in front to make the scheme name.
    // The composer, the login button and these options all use it.
    public const string SchemeName = "EntraId";
    public const string ProviderName = "Umbraco." + SchemeName;

    public void Configure(string? name, BackOfficeExternalLoginProviderOptions options)
    {
        if (name != ProviderName) return;

        var settings = entraOptions.Value;

        options.DenyLocalLogin = settings.DenyLocalLogin;

        // Groups are set in the callbacks below, so there are no default groups.
        options.AutoLinkOptions = new ExternalSignInAutoLinkOptions(
            autoLinkExternalAccount: true,
            defaultUserGroups: [],
            allowManualLinking: settings.AllowManualLinking)
        {
            // First login: runs before the new user is saved.
            OnAutoLinking = (user, info) =>
            {
                var aliases = ResolveGroups(info, settings);

                // Throwing here stops Umbraco from creating a user with no group.
                if (aliases.Count == 0)
                {
                    throw new InvalidOperationException("The Azure user has no role that maps to an Umbraco user group.");
                }

                user.IsApproved = true;
                ApplyGroups(user, aliases);
            },

            // Every login: keep the groups the same as in Azure.
            OnExternalLogin = (user, info) =>
            {
                var aliases = ResolveGroups(info, settings);
                if (aliases.Count == 0) return false;

                // If the groups cannot be saved, the login is refused, so nobody signs in with old groups.
                return !ApplyGroups(user, aliases) || Save(user);
            },
        };
    }

    public void Configure(BackOfficeExternalLoginProviderOptions options) => Configure(Options.DefaultName, options);

    // The Azure App Roles in the token, matched with the Umbraco groups that exist.
    private IReadOnlyCollection<string> ResolveGroups(ExternalLoginInfo info, EntraBackOfficeOptions settings)
    {
        var roles = EntraGroupMapper.RolesFrom(info.Principal);

        var services = httpContextAccessor.HttpContext!.RequestServices;
        var userGroupService = services.GetRequiredService<IUserGroupService>();
        var existingAliases = userGroupService.GetAllAsync(0, int.MaxValue)
            .GetAwaiter().GetResult()
            .Items
            .Select(g => g.Alias)
            .ToList();

        return EntraGroupMapper.Resolve(roles, existingAliases, settings);
    }

    // Sets the user's groups. Returns true if something changed.
    // Roles.Clear() and AddRole() are used so Umbraco knows the groups changed.
    private static bool ApplyGroups(BackOfficeIdentityUser user, IReadOnlyCollection<string> aliases)
    {
        var current = user.Roles.Select(r => r.RoleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (current.SetEquals(aliases)) return false;

        user.Roles.Clear();
        foreach (var alias in aliases)
        {
            user.AddRole(alias);
        }

        return true;
    }

    // Umbraco does not save changes made in the login callback, so save them here. False if the save failed.
    private bool Save(BackOfficeIdentityUser user)
    {
        var userManager = httpContextAccessor.HttpContext!.RequestServices.GetRequiredService<IBackOfficeUserManager>();
        var result = userManager.UpdateAsync(user).GetAwaiter().GetResult();

        if (!result.Succeeded)
        {
            logger.LogError(
                "Could not save the user groups for {User}: {Errors}",
                user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return result.Succeeded;
    }
}
