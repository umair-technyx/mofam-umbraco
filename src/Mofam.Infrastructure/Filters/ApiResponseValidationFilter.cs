using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;

namespace Mofam.Infrastructure.Filters;

// With [ApiController], ASP.NET answers a bad request body with its own 400 before the action runs.
// This filter changes that answer into the ApiResponse format. It only affects the controller it is put on.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiResponseValidationFilterAttribute : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ValidationProblemDetails problem }) return;

        // A key that starts with "$" is a field the JSON could not be read into (for example a field that does not exist).
        // The "request" key is a duplicate of the real problem.
        var errors = problem.Errors
            .Select(entry => (Name: entry.Key.TrimStart('$', '.'), FromJson: entry.Key.StartsWith('$'), entry.Value))
            .Where(entry => entry.Name.Length > 0 && !string.Equals(entry.Name, "request", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                entry => char.ToLowerInvariant(entry.Name[0]) + entry.Name[1..],
                entry => entry.FromJson ? new[] { string.Format(GeneralConstants.FieldInvalidMessage, entry.Name) } : entry.Value.Distinct().ToArray());

        var response = errors.Count == 0
            ? ApiResponse<object>.BadRequest("Request body is required.")
            : ApiResponse<object>.ValidationFailed(errors, "Please check the fields and try again.");

        context.Result = new ObjectResult(response with { TraceId = context.HttpContext.TraceIdentifier }) { StatusCode = StatusCodes.Status400BadRequest };
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
