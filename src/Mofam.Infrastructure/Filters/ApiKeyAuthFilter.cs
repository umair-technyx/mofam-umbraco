using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Options;

namespace Mofam.Infrastructure.Filters;

public sealed class ApiKeyAuthFilter(IOptions<SecurityOptions> options, Serilog.ILogger logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expectedKey = options.Value.ApiKey;

        if (string.IsNullOrEmpty(expectedKey))
        {
            context.Result = new ObjectResult(ApiResponse<object>.InternalServerError("API key not configured.") with { TraceId = context.HttpContext.TraceIdentifier }) { StatusCode = StatusCodes.Status500InternalServerError };
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

            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Unauthorized("Invalid or missing API key.") with { TraceId = context.HttpContext.TraceIdentifier });
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
