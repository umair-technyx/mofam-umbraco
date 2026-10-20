using System.Net;
using System.Text.Json.Serialization;

namespace Mofam.Domain.Models.Common;

public sealed record ApiResponse<T>
{
    public required HttpStatusCode StatusCode { get; init; }
    public required bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }

    // Field-level messages, keyed by the field name (for example "email"). Only sent when there are any.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { StatusCode = HttpStatusCode.OK, Success = true, Data = data, Message = message };

    public static ApiResponse<T> BadRequest(string message) =>
        new() { StatusCode = HttpStatusCode.BadRequest, Success = false, Message = message };

    public static ApiResponse<T> ValidationFailed(IReadOnlyDictionary<string, string[]> errors, string message) =>
        new() { StatusCode = HttpStatusCode.BadRequest, Success = false, Message = message, Errors = errors };

    public static ApiResponse<T> NotFound(string message) =>
        new() { StatusCode = HttpStatusCode.NotFound, Success = false, Message = message };

    public static ApiResponse<T> Forbidden(string message) =>
        new() { StatusCode = HttpStatusCode.Forbidden, Success = false, Message = message };

    public static ApiResponse<T> Unauthorized(string message) =>
        new() { StatusCode = HttpStatusCode.Unauthorized, Success = false, Message = message };

    public static ApiResponse<T> ExpectationFailed(string message) =>
        new() { StatusCode = HttpStatusCode.ExpectationFailed, Success = false, Message = message };

    public static ApiResponse<T> TooManyRequests(string message) =>
        new() { StatusCode = HttpStatusCode.TooManyRequests, Success = false, Message = message };

    public static ApiResponse<T> InternalServerError(string message) =>
        new() { StatusCode = HttpStatusCode.InternalServerError, Success = false, Message = message };

    public static ApiResponse<T> ServiceUnavailable(string message) =>
        new() { StatusCode = HttpStatusCode.ServiceUnavailable, Success = false, Message = message };
}
