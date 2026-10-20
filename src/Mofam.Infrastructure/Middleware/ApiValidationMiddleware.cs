using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Middleware;
public sealed class ApiValidationMiddleware(RequestDelegate next, Serilog.ILogger logger)
{
    private static readonly string[] BodyMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch];

    public async Task InvokeAsync(HttpContext context)
    {
        var hasBodyMethod = BodyMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase);

        // A bigger body is refused before it is read.
        if (hasBodyMethod)
        {
            const int maxBytes = GeneralConstants.MaxApiRequestBodyBytes;

            if (context.Request.ContentLength > maxBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                context.Response.ContentType = "application/json";
                var tooLargeResponse = ApiResponse<object>.BadRequest("Request body too large.") with { StatusCode = System.Net.HttpStatusCode.RequestEntityTooLarge };
                await context.Response.WriteAsync(JsonSerializer.Serialize(tooLargeResponse, ApiJson.Options));
                return;
            }

            // A request without a Content-Length (chunked) is cut at the limit while it is read.
            var sizeLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeLimit is { IsReadOnly: false })
            {
                sizeLimit.MaxRequestBodySize = maxBytes;
            }
        }

        var hasBody = hasBodyMethod && context.Request.ContentLength is > 0;

        if (hasBody)
        {
            var contentType = context.Request.ContentType?.Split(';')[0].Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(contentType) &&
                !contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                context.Response.ContentType = "application/json";
                var unsupportedResponse = ApiResponse<object>.BadRequest("Content-Type must be application/json.") with { StatusCode = System.Net.HttpStatusCode.UnsupportedMediaType };
                await context.Response.WriteAsync(JsonSerializer.Serialize(unsupportedResponse, ApiJson.Options));
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
                    using var document = JsonDocument.Parse(body);
                }
                catch (JsonException ex)
                {
                    // The body is not logged, because it can hold personal data (for example the Forms).
                    logger.Warning(
                        "Malformed JSON request body. Path={Path}, Method={Method}, Length={Length}, Error={Error}",
                        context.Request.Path,
                        context.Request.Method,
                        context.Request.ContentLength,
                        ex.Message);

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    var malformedResponse = ApiResponse<object>.BadRequest("Invalid request body.");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(malformedResponse, ApiJson.Options));
                    return;
                }
            }
        }

        await next(context);
    }
}
