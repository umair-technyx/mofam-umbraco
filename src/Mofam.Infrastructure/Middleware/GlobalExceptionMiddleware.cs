using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // The client gets only a generic message and the trace id. The real error is written to the log.
            logger.LogError(ex, "Unhandled exception. TraceId {TraceId}, path {Path}", context.TraceIdentifier, context.Request.Path);

            var (statusCode, message) = ex switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid request."),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
            };

            var response = new ApiResponse<object>
            {
                StatusCode = statusCode,
                Success = false,
                Message = message,
                TraceId = context.TraceIdentifier,
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
