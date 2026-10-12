using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Application.IServices;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
public sealed class SearchController(ISiteSearchService searchService) : ControllerBase
{
    // Content search using Examine. Paging and sorting are done in the index.
    [HttpPost("search")]
    public ActionResult<ApiResponse<SearchResultsDto>> Search([FromBody] SearchRequest request)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse<SearchResultsDto>.BadRequest("A search request body is required."));
        }

        var results = searchService.Search(request);
        return Ok(ApiResponse<SearchResultsDto>.Ok(results, "Search completed successfully."));
    }
}
