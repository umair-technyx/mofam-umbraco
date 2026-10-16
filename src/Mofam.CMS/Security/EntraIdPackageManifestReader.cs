using Microsoft.Extensions.Options;
using Mofam.Domain.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Mofam.CMS.Security;

// Gives the backoffice login page the "Sign in with Microsoft" button.
// It is registered only when the Entra login is enabled, so the button is not there when it is off.
public sealed class EntraIdPackageManifestReader(IOptions<EntraBackOfficeOptions> options) : IPackageManifestReader
{
    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        var manifest = new PackageManifest
        {
            Name = "Mofam Entra ID Login",
            Id = "Mofam.EntraIdLogin",

            // The login page is shown before sign in, so the manifest must be public.
            AllowPublicAccess = true,

            Extensions =
            [
                new Dictionary<string, object?>
                {
                    ["type"] = "authProvider",
                    ["alias"] = "Mofam.EntraIdAuthProvider",
                    ["name"] = "Entra ID Auth Provider",
                    ["forProviderName"] = EntraIdBackOfficeLoginOptions.ProviderName,
                    ["meta"] = new Dictionary<string, object?>
                    {
                        ["label"] = "Microsoft",
                        ["defaultView"] = new Dictionary<string, object?>
                        {
                            ["icon"] = "icon-microsoft",
                            ["look"] = "primary",
                        },

                        // true: the login page goes straight to Microsoft, with no button.
                        ["behavior"] = new Dictionary<string, object?>
                        {
                            ["autoRedirect"] = options.Value.AutoRedirect,
                        },

                        // Same setting as on the server side.
                        ["linking"] = new Dictionary<string, object?>
                        {
                            ["allowManualLinking"] = options.Value.AllowManualLinking,
                        },
                    },
                },
            ],
        };

        return Task.FromResult<IEnumerable<PackageManifest>>([manifest]);
    }
}
