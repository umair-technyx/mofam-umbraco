using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Application.Abstractions;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers;

[ApiController]
[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
public sealed class WebApiController(
    IApiService apiService,
    IStartupService startupService,
    IFilterService filterService,
    ISiteSearchService searchService) : ControllerBase
{
    [HttpGet("pages/{culture}/{slug}")]
    public ActionResult<ApiResponse<PageDto>> GetBySlug(string culture, string slug)
    {
        var page = apiService.GetPageBySlug(
            CmsConstants.ContentTypes.Page,
            slug,
            culture);

        return page is null
            ? NotFound(ApiResponse<PageDto>.NotFound("Page not found."))
            : Ok(ApiResponse<PageDto>.Ok(page, "Page fetched successfully."));
    }

    /// <summary>Header, footer, dictionary and culture in a single first-load call.</summary>
    [HttpGet("{culture}/startup")]
    public async Task<ActionResult<ApiResponse<Startup>>> GetStartup(string culture)
    {
        var startup = await startupService.GetStartupAsync(culture);

        return startup is null
            ? NotFound(ApiResponse<Startup>.NotFound("Site root not found."))
            : Ok(ApiResponse<Startup>.Ok(startup, "Startup fetched successfully."));
    }

    /// <summary>
    /// Available filter options for a listing type, e.g. the categories actually used by
    /// published services. Options with no matching content are never returned.
    /// </summary>
    [HttpGet("{culture}/filters")]
    public ActionResult<ApiResponse<FilterDataDto>> GetFilterData(string culture, [FromQuery] string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return BadRequest(ApiResponse<FilterDataDto>.BadRequest("A contentType is required."));
        }

        var data = filterService.GetFilterData(contentType, culture);

        return data is null
            ? NotFound(ApiResponse<FilterDataDto>.NotFound("No filter data found."))
            : Ok(ApiResponse<FilterDataDto>.Ok(data, "Filter data fetched successfully."));
    }

    /// <summary>Examine-backed content search. Paging and sorting happen in the index.</summary>
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
