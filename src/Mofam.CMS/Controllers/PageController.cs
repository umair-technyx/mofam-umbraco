using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Application.Abstractions;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers;

[ApiController]
[Route("api/v1/web/pages")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
public sealed class PageController(IPageService pageService) : ControllerBase
{
    [HttpGet("{culture}/{slug}")]
    public ActionResult<ApiResponse<PageDto>> GetBySlug(string culture, string slug)
    {
        var page = pageService.GetPageBySlug(
            CmsConstants.ContentTypes.WebPage,
            slug,
            culture);

        return page is null
            ? NotFound(ApiResponse<PageDto>.NotFound("Page not found."))
            : Ok(ApiResponse<PageDto>.Ok(page, "Page fetched successfully."));
    }
}
