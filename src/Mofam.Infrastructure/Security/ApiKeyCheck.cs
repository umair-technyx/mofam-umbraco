using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Constants;

namespace Mofam.Infrastructure.Security;

public static class ApiKeyCheck
{
    // True when the request carries the expected API key. An empty expected key never matches.
    public static bool HasValidKey(HttpContext context, string? expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey)) return false;
        if (!context.Request.Headers.TryGetValue(CmsConstants.Http.ApiKeyHeader, out var providedKey)) return false;

        return IsMatch(providedKey.ToString(), expectedKey);
    }

    // Constant time, so the time taken does not tell how much of a guessed key was right.
    public static bool IsMatch(string providedKey, string expectedKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        if (providedBytes.Length != expectedBytes.Length) return false;

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
