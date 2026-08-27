using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Middleware;
public sealed class ApiValidationMiddleware(RequestDelegate next)
{
    private static readonly string[] BodyMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch];

    public async Task InvokeAsync(HttpContext context)
    {
        var hasBody = BodyMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase)
            && context.Request.ContentLength is > 0;

        if (hasBody)
        {
            var contentType = context.Request.ContentType?.Split(';')[0].Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(contentType) &&
                !contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                context.Response.ContentType = "application/json";
                var unsupportedResponse = ApiResponse<object>.BadRequest("Content-Type must be application/json.");
                await context.Response.WriteAsync(JsonSerializer.Serialize(unsupportedResponse));
                return;
            }

            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    JsonDocument.Parse(body);
                }
                catch (JsonException ex)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    var malformedResponse = ApiResponse<object>.BadRequest($"Malformed JSON: {ex.Message}");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(malformedResponse));
                    return;
                }
            }
        }

        await next(context);
    }
}
