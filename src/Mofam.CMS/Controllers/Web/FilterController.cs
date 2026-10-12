using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
public sealed class FilterController(IFilterService filterService) : ControllerBase
{
    // Filter options for a listing type, for example the categories used by published
    // services. Options with no content are not returned.
    [HttpGet("{culture}/filters")]
    public ActionResult<ApiResponse<FilterDataDto>> GetFilterData(string culture, [FromQuery] string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return BadRequest(ApiResponse<FilterDataDto>.BadRequest("A contentType is required."));
        }

        // Unknown cultures never reach the cache, and "EN" / "en" share one entry.
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return NotFound(ApiResponse<FilterDataDto>.NotFound("Culture not supported."));
        }

        var data = filterService.GetFilterData(contentType, supported);

        return data is null
            ? NotFound(ApiResponse<FilterDataDto>.NotFound("No filter data found."))
            : Ok(ApiResponse<FilterDataDto>.Ok(data, "Filter data fetched successfully."));
    }
}
