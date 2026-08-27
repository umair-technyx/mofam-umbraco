using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;

namespace Mofam.Infrastructure.Filters;

public sealed class ApiKeyAuthFilter(IOptions<SecurityOptions> options, Serilog.ILogger logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expectedKey = options.Value.ApiKey;

        if (string.IsNullOrEmpty(expectedKey))
        {
            context.Result = new ObjectResult("API key not configured.") { StatusCode = 500 };
            return;
        }

        var hasHeader = context.HttpContext.Request.Headers.TryGetValue(CmsConstants.Http.ApiKeyHeader, out var providedKeyValue);
        var providedKey = providedKeyValue.ToString();

        if (!hasHeader || !IsMatch(providedKey, expectedKey))
        {
            logger.Warning(
                "Rejected API request — invalid or missing API key. Path={Path}, RemoteIp={RemoteIp}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new UnauthorizedObjectResult("Invalid or missing API key.");
            return;
        }

        await next();
    }
    private static bool IsMatch(string providedKey, string expectedKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        if (providedBytes.Length != expectedBytes.Length) return false;

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
