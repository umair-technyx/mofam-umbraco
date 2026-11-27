using System.Net;
using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Forms;

[ApiController]
[Route("api/web/form")]
[ApiResponseValidationFilter]
public sealed class ContactController(IContactService contactService) : ControllerBase
{
    [HttpPost("{culture}/contact")]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<object>>> Submit(string culture, [FromBody] ContactRequest request, CancellationToken cancellationToken)
    {
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Culture not supported."));
        }

        // The request id is the reference of the submission in the logs; it is sent back as the traceId.
        await contactService.SubmitAsync(request, supported, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            StatusCode = HttpStatusCode.OK,
            Success = true,
            Message = "Your message has been sent successfully.",
            TraceId = HttpContext.TraceIdentifier,
        });
    }
}
