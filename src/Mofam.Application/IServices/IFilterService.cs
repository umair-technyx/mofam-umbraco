using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.IServices;

// Builds the filter options for a listing type from published content,
// so an option with no matching items never appears.
public interface IFilterService
{
    #region Filters

    // Returns null if the content type has no filters set up.
    FilterDataDto? GetFilterData(string contentType, string? culture);

    #endregion
}
