using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
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
            return BadRequest(ApiResponse<FilterDataDto>.BadRequest("Culture not supported."));
        }

        // A content type without filters is a wrong request, not an empty result.
        if (!SearchConstants.FilterDefinitions.ContainsKey(contentType.Trim()))
        {
            return BadRequest(ApiResponse<FilterDataDto>.BadRequest("Content type not supported."));
        }

        // A content type that has filters but no values gives an empty list (200). Null here means the server failed (the error is logged in the service).
        var data = filterService.GetFilterData(contentType.Trim(), supported);

        return data is null
            ? StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<FilterDataDto>.InternalServerError(GeneralConstants.UnexpectedErrorMessage))
            : Ok(ApiResponse<FilterDataDto>.Ok(data, "Filter data fetched successfully."));
    }
}
