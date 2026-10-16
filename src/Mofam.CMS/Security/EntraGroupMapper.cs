using System.Security.Claims;
using Mofam.Domain.Options;

namespace Mofam.CMS.Security;

// Works out the Umbraco user groups from the Azure App Roles in the login token.
// An Azure role value that equals an existing Umbraco group alias gives that group.
// A new role needs no code change, only a group with the same alias in Umbraco.
public static class EntraGroupMapper
{
    public static IReadOnlyCollection<string> Resolve(
        IEnumerable<string> azureRoles,
        IReadOnlyCollection<string> existingAliases,
        EntraBackOfficeOptions options)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in azureRoles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()))
        {
            // The Azure role value is the Umbraco group alias.
            AddIfExists(result, role, existingAliases);

            // Extra groups for this role, from the config.
            if (options.ExtraGroups.TryGetValue(role, out var extras))
            {
                foreach (var extra in extras)
                {
                    AddIfExists(result, extra, existingAliases);
                }
            }
        }

        // No role matched: the result is empty and the user is not let in. There are no default groups.
        return result;
    }

    // The Azure App Role values in the login token.
    public static IReadOnlyList<string> RolesFrom(ClaimsPrincipal principal) =>
        principal.Claims
            .Where(c => c.Type is "roles" or "role" or ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

    // Adds the alias (as Umbraco spells it) only if the group exists.
    private static void AddIfExists(HashSet<string> result, string alias, IReadOnlyCollection<string> existingAliases)
    {
        var match = existingAliases.FirstOrDefault(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase));
        if (match is not null) result.Add(match);
    }
}
