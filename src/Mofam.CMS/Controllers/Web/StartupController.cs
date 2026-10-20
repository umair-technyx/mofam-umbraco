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
public sealed class StartupController(IStartupService startupService) : ControllerBase
{
    [HttpGet("{culture}/startup")]
    public async Task<ActionResult<ApiResponse<Startup>>> GetStartup(string culture)
    {
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return NotFound(ApiResponse<Startup>.NotFound("Culture not supported."));
        }

        var startup = await startupService.GetStartupAsync(supported);

        return startup is null
            ? NotFound(ApiResponse<Startup>.NotFound("Site root not found."))
            : Ok(ApiResponse<Startup>.Ok(startup, "Startup fetched successfully."));
    }
}
