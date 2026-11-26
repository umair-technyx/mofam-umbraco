using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;

namespace Mofam.CMS.Controllers.Web;

[ApiController]
[Route("api/web")]
public sealed class StartupController(IStartupService startupService) : ControllerBase
{
    [HttpGet("{culture}/startup")]
    public async Task<ActionResult<ApiResponse<Startup>>> GetStartup(string culture)
    {
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return BadRequest(ApiResponse<Startup>.BadRequest("Culture not supported."));
        }

        var startup = await startupService.GetStartupAsync(supported);

        return startup is null
            ? NotFound(ApiResponse<Startup>.NotFound("Site root not found."))
            : Ok(ApiResponse<Startup>.Ok(startup, "Startup fetched successfully."));
    }
}
