using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;

namespace Mofam.CMS.Controllers.Forms;

[VersionedApiBackOfficeRoute("mofam/form-submission")]
[Authorize(Policy = "RequireAdminAccess")]
public sealed class FormExportController(IContactSubmissionService contactService, INewsletterSubscriptionService newsletterService) : ManagementApiControllerBase
{
    [HttpGet("export-contact-submissions")]
    public async Task<IActionResult> ExportContactSubmissions([FromQuery] string? culture)
    {
        var supported = CmsConstants.Cultures.Resolve(culture?.Trim());
        if (supported is null) return BadRequest(ApiResponse<object>.BadRequest("Culture is required and must be en or ar."));

        var rows = await contactService.GetAllAsync(supported);
        if (rows.Count == 0) return NoContent();

        return File(
            CommonHelper.BuildCsv(ExportConstants.ContactHeaders, rows.Select(r => r.ToCsvRow()), ExportConstants.ContactPhoneColumn),
            "text/csv; charset=utf-8",
            $"contact-submissions-{supported}-{DateTime.UtcNow.AddHours(4).ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv");
    }

    [HttpGet("export-newsletter-subscribers")]
    public async Task<IActionResult> ExportNewsletterSubscribers([FromQuery] string? culture)
    {
        var supported = CmsConstants.Cultures.Resolve(culture?.Trim());
        if (supported is null) return BadRequest(ApiResponse<object>.BadRequest("Culture is required and must be en or ar."));

        var rows = await newsletterService.GetAllAsync(supported);
        if (rows.Count == 0) return NoContent();

        return File(
            CommonHelper.BuildCsv(ExportConstants.NewsletterHeaders, rows.Select(r => r.ToCsvRow())),
            "text/csv; charset=utf-8",
            $"newsletter-subscribers-{supported}-{DateTime.UtcNow.AddHours(4).ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv");
    }
}
