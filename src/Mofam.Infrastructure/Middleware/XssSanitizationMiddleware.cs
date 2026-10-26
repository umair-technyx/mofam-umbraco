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
        // The forms (/api/web/form/...) are not checked here: their service removes the HTML and encodes the text, and this check can refuse normal messages.
        var isForm = context.Request.Path.StartsWithSegments("/api/web/form", StringComparison.OrdinalIgnoreCase);

        var isJsonBodyRequest =
            !isForm &&
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
                        await context.Response.WriteAsync(JsonSerializer.Serialize(response, ApiJson.Options));
                        return;
                    }
                }
                catch (JsonException)
                {
                    // Malformed JSON — ApiValidationMiddleware/model binding handles this, so we can ignore it here.
                }
                catch (RegexMatchTimeoutException)
                {
                    // A body that takes this long to check is not a normal request: refuse it as a 400, not a 500.
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    var response = ApiResponse<object>.BadRequest("Request rejected: malicious input detected.");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response, ApiJson.Options));
                    return;
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

    // NonBacktracking runs in linear time, so a crafted body cannot make these checks slow.
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.NonBacktracking;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex[] Patterns =
    [
        new(@"<script\b.*?</script>", Options | RegexOptions.Singleline, MatchTimeout),
        // Only inside a tag: plain text such as "Online = yes" is not an event handler.
        new(@"<[^>]*\bon\w+\s*=", Options, MatchTimeout),
        new(@"javascript:", Options, MatchTimeout),
    ];

    private static bool IsMaliciousString(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        return Patterns.Any(p => p.IsMatch(input));
    }
}
