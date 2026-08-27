using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace Mofam.Infrastructure.Routing;
public sealed class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value is null) return null;

        return Regex.Replace(
            value.ToString()!,
            "([a-z])([A-Z])",
            "$1-$2",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(100)).ToLowerInvariant();
    }
}
