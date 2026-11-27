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
public sealed class NewsletterController(INewsletterService newsletterService) : ControllerBase
{
    [HttpPost("{culture}/newsletter")]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<NewsletterReceiptDto>>> Subscribe(string culture, [FromBody] NewsletterRequest request, CancellationToken cancellationToken)
    {
        var supported = CmsConstants.Cultures.Resolve(culture);
        if (supported is null)
        {
            return BadRequest(ApiResponse<NewsletterReceiptDto>.BadRequest("Culture not supported."));
        }

        var receipt = await newsletterService.SubscribeAsync(request, supported, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier, cancellationToken);

        var message = receipt.AlreadySubscribed
            ? "You are already subscribed to the newsletter."
            : "You have subscribed to the newsletter successfully.";

        return Ok(ApiResponse<NewsletterReceiptDto>.Ok(receipt, message) with { TraceId = receipt.Reference });
    }
}
