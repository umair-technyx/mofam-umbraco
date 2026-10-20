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
public sealed class PageController(IApiService apiService) : ControllerBase
{
    [HttpGet("{culture}/{contentType}/{slug}")]
    public ActionResult<ApiResponse<PageDto>> GetPageData(string culture, string contentType, string slug)
    {
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return NotFound(ApiResponse<PageDto>.NotFound("Culture not supported."));
        }

        var page = apiService.GetPageBySlug(contentType, slug, supported);

        return page is null
            ? NotFound(ApiResponse<PageDto>.NotFound("Page not found."))
            : Ok(ApiResponse<PageDto>.Ok(page, "Page fetched successfully."));
    }
}
