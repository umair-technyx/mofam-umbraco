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
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public sealed class SearchController(ISiteSearchService searchService) : ControllerBase
{
    [HttpPost("search")]
    public ActionResult<ApiResponse<SearchResultsDto>> Search([FromBody] SearchRequest request)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse<SearchResultsDto>.BadRequest("A search request body is required."));
        }

        if (!string.IsNullOrWhiteSpace(request.Culture))
        {
            var supported = CmsConstants.Cultures.Resolve(request.Culture.Trim());
            if (supported is null)
            {
                return BadRequest(ApiResponse<SearchResultsDto>.BadRequest("Culture not supported."));
            }

            request = request with { Culture = supported };
        }

        var results = searchService.Search(request);
        return Ok(ApiResponse<SearchResultsDto>.Ok(results, "Search completed successfully."));
    }
}
