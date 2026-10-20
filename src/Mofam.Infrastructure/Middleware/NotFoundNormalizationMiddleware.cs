using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Middleware;
public sealed class NotFoundNormalizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        catch
        {
            // The real body is put back before the exception moves on. Otherwise the exception middleware would write
            // into the buffer that is disposed here, and the client would get an empty 500 instead of the error response.
            context.Response.Body = originalBody;
            throw;
        }

        if (context.Response.StatusCode == StatusCodes.Status404NotFound && buffer.Length == 0)
        {
            var response = new ApiResponse<object>
            {
                StatusCode = HttpStatusCode.NotFound,
                Success = false,
                Message = "The requested resource was not found.",
                TraceId = context.TraceIdentifier,
            };

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, ApiJson.Options));
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = null;
            await originalBody.WriteAsync(bytes);
        }
        else
        {
            // The buffered bytes may still be compressed downstream, so any Content-Length
            // set while writing into the buffer no longer describes what goes over the wire.
            context.Response.ContentLength = null;
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }

        context.Response.Body = originalBody;
    }
}
