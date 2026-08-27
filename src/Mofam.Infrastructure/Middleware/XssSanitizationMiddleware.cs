using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Middleware;
public sealed class XssSanitizationMiddleware(RequestDelegate next)
{
    private static readonly string[] BodyMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch];

    public async Task InvokeAsync(HttpContext context)
    {
        var isJsonBodyRequest =
            BodyMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase) &&
            (context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (isJsonBodyRequest)
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var jsonDocument = JsonDocument.Parse(body);
                    if (IsMaliciousJson(jsonDocument.RootElement))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "application/json";
                        var response = ApiResponse<object>.BadRequest("Request rejected: malicious input detected.");
                        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                        return;
                    }
                }
                catch (JsonException)
                {
                    // Malformed JSON — ApiValidationMiddleware/model binding handles this, so we can ignore it here.
                }
            }
        }

        await next(context);
    }

    private static bool IsMaliciousJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(p => IsMaliciousJson(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(IsMaliciousJson),
        JsonValueKind.String => IsMaliciousString(element.GetString() ?? string.Empty),
        _ => false,
    };

    private static bool IsMaliciousString(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        return Regex.IsMatch(input, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))
            || Regex.IsMatch(input, @"on\w+\s*=\s*[""']?[^""'>\s]*[""']?", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))
            || Regex.IsMatch(input, @"javascript:", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }
}
