using Microsoft.AspNetCore.DataProtection;
using Mofam.Domain.Constants;

namespace Mofam.CMS.Security;

// Makes and reads the token in the address of the sign in error page.
// The token holds the reason and the reference. It is encrypted and expires,
// so nobody can read it, change it, or open the page without it.
public sealed class SignInErrorToken(IDataProtectionProvider provider)
{
    private const char Separator = '|';

    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector(GeneralConstants.SignInErrorTokenPurpose).ToTimeLimitedDataProtector();

    // The address to send the user to, with the token in it.
    public string UrlFor(string reason, string? reference)
    {
        var token = _protector.Protect(
            $"{reason}{Separator}{reference}",
            TimeSpan.FromMinutes(GeneralConstants.SignInErrorTokenMinutes));

        return $"{GeneralConstants.SignInErrorPath}?{GeneralConstants.SignInErrorTokenQuery}={Uri.EscapeDataString(token)}";
    }

    // False if the token is missing, wrong, changed or expired.
    public bool TryRead(string? token, out string reason, out string? reference)
    {
        reason = GeneralConstants.Failed;
        reference = null;

        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var parts = _protector.Unprotect(token).Split(Separator, 2);

            reason = parts[0];
            reference = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
