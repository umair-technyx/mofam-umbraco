using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public sealed class FilterController(IFilterService filterService) : ControllerBase
{
    [HttpGet("{culture}/filters")]
    public ActionResult<ApiResponse<FilterDataDto>> GetFilterData(string culture, [FromQuery] string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return BadRequest(ApiResponse<FilterDataDto>.BadRequest("A contentType is required."));
        }

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
