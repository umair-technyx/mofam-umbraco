using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;
using Mofam.Infrastructure.Security;

namespace Mofam.Infrastructure.RateLimiting;

// Who the rate limit counts a request against.
// Key: the visitor's address. IsOurServer: the request has the API key but names no visitor (the front end server rendering a page).
public readonly record struct ClientAddress(string Key, bool IsOurServer)
{
    public static bool HasValidKey(HttpContext context) =>
        ApiKeyCheck.HasValidKey(context, context.RequestServices.GetService<IOptions<SecurityOptions>>()?.Value.ApiKey);

    public static ClientAddress Resolve(HttpContext context)
    {
        var hasKey = HasValidKey(context);

        // The front end server sends the visitor's address. Anyone can send this header, so it counts only with the correct API key.
        if (hasKey && IPAddress.TryParse(context.Request.Headers[CmsConstants.Http.ClientIpHeader].ToString().Trim(), out var visitor))
        {
            return new ClientAddress(Normalize(visitor), false);
        }

        var remote = context.Connection.RemoteIpAddress;
        return new ClientAddress(remote is null ? "unknown" : Normalize(remote), hasKey);
    }

    // One home or phone gets a whole IPv6 /64 block and can change the last part at will, so the block is counted as one visitor.
    private static string Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || IPAddress.IsLoopback(address)) return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
