using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Abstractions;

/// <summary>
/// Builds the available filter options for a listing type, derived from published
/// content so options with no matching items never appear.
/// </summary>
public interface IFilterService
{
    /// <summary>Returns null when the type has no configured filters.</summary>
    FilterDataDto? GetFilterData(string contentType, string? culture);
}
