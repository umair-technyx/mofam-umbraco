using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Application.Abstractions;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers;

[ApiController]
[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
// Search is currently disabled — IContentSearchService is not registered in
// ServiceComposer for now
public sealed class WebApiController(
    IApiService apiService,
    IStartupService startupService) : ControllerBase
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

    /// <summary>Examine-backed content search. Paging and sorting happen in the index.</summary>
    //[HttpPost("search")]
    //public ActionResult<ApiResponse<SearchResultsDto>> Search([FromBody] SearchRequest request)
    //{
    //    if (request is null)
    //    {
    //        return BadRequest(ApiResponse<SearchResultsDto>.BadRequest("A search request body is required."));
    //    }

    //    var results = searchService.Search(request);
    //    return Ok(ApiResponse<SearchResultsDto>.Ok(results, "Search completed successfully."));
    //}
}
