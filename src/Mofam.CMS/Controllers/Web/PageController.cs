using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Application.IServices;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
public sealed class PageController(IApiService apiService) : ControllerBase
{
    // Detail of any allowed content type (page, service, etc).
    // The front end maps its URL to a content type: /services/x -> service/x.
    [HttpGet("{culture}/{contentType}/{slug}")]
    public ActionResult<ApiResponse<PageDto>> GetPageData(string culture, string contentType, string slug)
    {
        var page = apiService.GetPageBySlug(contentType, slug, culture);

        return page is null
            ? NotFound(ApiResponse<PageDto>.NotFound("Page not found."))
            : Ok(ApiResponse<PageDto>.Ok(page, "Page fetched successfully."));
    }
}
