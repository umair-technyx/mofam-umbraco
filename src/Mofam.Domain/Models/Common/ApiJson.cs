using System.Text.Json;

namespace Mofam.Domain.Models.Common;

// The JSON settings for an ApiResponse that is written by hand (middleware). They are the same as the controllers use (camelCase),
// so every API response looks the same, whoever writes it.
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
