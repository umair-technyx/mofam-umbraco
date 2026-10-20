using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Exceptions;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, Serilog.ILogger logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (HttpStatusException ex)
        {
            // An expected failure (for example 400 or 503). It was written to the log where it happened.
            await WriteAsync(context, ex.StatusCode, ex.Message, ex.Errors, context.TraceIdentifier);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The visitor closed the page or lost the connection. This is normal: nobody reads a response, so nothing is logged and no 500 is written.
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            // The client gets only a generic message and the trace id. The real error is written to the log.
            logger.Error(ex, "Unhandled exception. TraceId {TraceId}, path {Path}", context.TraceIdentifier, context.Request.Path);

            var (statusCode, message) = ex switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid request."),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
                BadHttpRequestException bad => ((HttpStatusCode)bad.StatusCode, bad.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Request body too large." : "Bad request."),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
            };

            await WriteAsync(context, statusCode, message, null, context.TraceIdentifier);
        }
    }

    private static async Task WriteAsync(
        HttpContext context, HttpStatusCode statusCode, string message, IReadOnlyDictionary<string, string[]>? errors, string traceId)
    {
        var response = new ApiResponse<object>
        {
            StatusCode = statusCode,
            Success = false,
            Message = message,
            TraceId = traceId,
            Errors = errors,
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, ApiJson.Options));
    }
}
