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
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[ApiResponseValidationFilter]
public sealed class FormsController(IContactService contactService) : ControllerBase
{
    [HttpPost("{culture}/contact")]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<ContactReceiptDto>>> Submit(string culture, [FromBody] ContactRequest request, CancellationToken cancellationToken)
    {
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return BadRequest(ApiResponse<ContactReceiptDto>.BadRequest("Culture not supported."));
        }

        // A bad body (empty, a wrong field, a field that fails its rule) is answered by ApiResponseValidationFilter before this runs.
        var receipt = await contactService.SubmitAsync(request, supported, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier, cancellationToken);

        return Ok(ApiResponse<ContactReceiptDto>.Ok(receipt, "Your message has been sent successfully.") with { TraceId = receipt.Reference });
    }
}
