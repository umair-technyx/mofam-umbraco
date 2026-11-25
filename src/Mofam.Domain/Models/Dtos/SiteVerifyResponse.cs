using System.Text.Json.Serialization;

namespace Mofam.Domain.Models.Dtos;

/// <summary>The JSON answer of the reCAPTCHA siteverify endpoint.</summary>
public sealed record SiteVerifyResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("score")] double? Score,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("error-codes")] List<string>? ErrorCodes);
