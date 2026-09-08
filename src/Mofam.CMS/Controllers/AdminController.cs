using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mofam.Domain.Models.Common;
using Mofam.Infrastructure.Filters;
using Serilog;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Mofam.CMS.Controllers;

[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[EnableRateLimiting("api")]
public sealed class AdminController(
    IUmbracoDatabaseFactory dbFactory,
    Serilog.ILogger logger) : ControllerBase
{
    private const string AdminGroupAlias = "admin";

    /// <summary>
    /// Promotes a backoffice user to the admin group via GET.
    /// </summary>
    [HttpGet("make-admin")]
    public ActionResult<ApiResponse<string>> MakeAdmin([FromQuery] string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(ApiResponse<string>.BadRequest("A valid email parameter is required."));
        }

        var normalizedEmail = email.Trim();

        try
        {
            using var db = dbFactory.CreateDatabase();

            // Step 1: Look up user ID
            var userId = db.ExecuteScalar<int?>(
                "SELECT id FROM umbracoUser WHERE userEmail = @0", normalizedEmail);

            if (userId is null)
            {
                logger.Warning("MakeAdmin failed: User with email {Email} not found.", normalizedEmail);
                return NotFound(ApiResponse<string>.NotFound($"User with email '{normalizedEmail}' not found."));
            }

            // Step 2: Look up admin group ID
            var adminGroupId = db.ExecuteScalar<int?>(
                "SELECT id FROM umbracoUserGroup WHERE userGroupAlias = @0", AdminGroupAlias);

            if (adminGroupId is null)
            {
                logger.Error("MakeAdmin failed: Admin group '{GroupAlias}' not found in database.", AdminGroupAlias);
                return NotFound(ApiResponse<string>.NotFound($"Admin group '{AdminGroupAlias}' not found."));
            }

            // Step 3: Check if relation already exists
            var isAlreadyAdmin = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM umbracoUser2UserGroup WHERE userId = @0 AND userGroupId = @1",
                userId.Value, adminGroupId.Value);

            if (isAlreadyAdmin > 0)
            {
                logger.Information("MakeAdmin: User {Email} is already in the admin group.", normalizedEmail);
                return Ok(ApiResponse<string>.Ok($"User '{normalizedEmail}' is already an admin."));
            }

            // Step 4: Insert user-to-group relation
            db.Execute(
                "INSERT INTO umbracoUser2UserGroup (userId, userGroupId) VALUES (@0, @1)",
                userId.Value, adminGroupId.Value);

            logger.Information("MakeAdmin: User {Email} successfully promoted to admin.", normalizedEmail);
            return Ok(ApiResponse<string>.Ok($"User '{normalizedEmail}' successfully promoted to admin."));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MakeAdmin failed with an unexpected error for {Email}.", normalizedEmail);
            return StatusCode(500, ApiResponse<string>.InternalServerError("An error occurred while promoting user to admin."));
        }
    }
}
