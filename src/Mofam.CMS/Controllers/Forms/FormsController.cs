using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Forms;

[Route("api/web/form")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
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

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(entry => entry.Key != nameof(request) && entry.Key.TrimStart('$', '.').Length > 0 && entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => char.ToLowerInvariant(entry.Key.TrimStart('$', '.')[0]) + entry.Key.TrimStart('$', '.')[1..],
                    entry => entry.Value!.Errors
                        .Select(error => !entry.Key.StartsWith('$') ? error.ErrorMessage : string.Format(GeneralConstants.FieldInvalidMessage, entry.Key.TrimStart('$', '.')))
                        .Distinct().ToArray());

            if (errors.Count == 0)
            {
                return BadRequest(ApiResponse<ContactReceiptDto>.BadRequest("Request body is required.") with { TraceId = HttpContext.TraceIdentifier });
            }

            return BadRequest(ApiResponse<ContactReceiptDto>.ValidationFailed(errors, "Please check the fields and try again.") with { TraceId = HttpContext.TraceIdentifier });
        }

        var receipt = await contactService.SubmitAsync(request, supported, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier, cancellationToken);

        return Ok(ApiResponse<ContactReceiptDto>.Ok(receipt, "Your message has been sent successfully.") with { TraceId = receipt.Reference });
    }
}
