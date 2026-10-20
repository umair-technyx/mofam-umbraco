using System.Net;

namespace Mofam.Domain.Exceptions;
public sealed class HttpStatusException(HttpStatusCode statusCode, string message, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}
