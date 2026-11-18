using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Options;
using Mofam.Infrastructure.Security;

namespace Mofam.Infrastructure.Middleware;

// Checks the API key before anything else reads the request. A caller without the key gets 401 at once:
// the body is not read, and no validation message tells them which fields the API expects.
public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<SecurityOptions> options, Serilog.ILogger logger)
    {
        // A CORS preflight never carries the key; the CORS middleware answers it before this runs.
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var expectedKey = options.Value.ApiKey;

        if (string.IsNullOrEmpty(expectedKey))
        {
            await Write(context, StatusCodes.Status500InternalServerError, ApiResponse<object>.InternalServerError("API key not configured."));
            return;
        }

        if (!ApiKeyCheck.HasValidKey(context, expectedKey))
        {
            // Information, not Warning: Warning goes to Sentry, and anyone can send requests without the key.
            logger.Information(
                "Rejected API request — invalid or missing API key. Path={Path}, RemoteIp={RemoteIp}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            await Write(context, StatusCodes.Status401Unauthorized, ApiResponse<object>.Unauthorized("Invalid or missing API key."));
            return;
        }

        await next(context);
    }

    private static Task Write(HttpContext context, int statusCode, ApiResponse<object> response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(response with { TraceId = context.TraceIdentifier }, ApiJson.Options));
    }
}
