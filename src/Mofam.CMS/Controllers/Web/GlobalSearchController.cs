using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
public sealed class GlobalSearchController(IGlobalSearchService globalSearchService) : ControllerBase
{
    [HttpPost("global-search")]
    [ApiResponseValidationFilter]
    public ActionResult<ApiResponse<GlobalSearchDto>> GetGlobalSearch([FromBody] GlobalSearchRequest request)
    {
        var supported = CmsConstants.Cultures.Resolve(request.Culture?.Trim());
        if (supported is null)
        {
            return BadRequest(ApiResponse<GlobalSearchDto>.BadRequest("Culture not supported."));
        }

        request = request with { Culture = supported };

        return Ok(ApiResponse<GlobalSearchDto>.Ok(globalSearchService.GetGlobalSearch(request), "Global search completed successfully."));
    }
}
