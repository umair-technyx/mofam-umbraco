using Microsoft.AspNetCore.Mvc;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Infrastructure.Filters;

namespace Mofam.CMS.Controllers.Web;

[Route("api/web")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public sealed class SearchController(ISiteSearchService searchService) : ControllerBase
{
    [HttpPost("search")]
    public ActionResult<ApiResponse<SearchResultsDto>> Search([FromBody] SearchRequest request)
    {
        if (!ModelState.IsValid)
        {
            var unknown = ModelState
                .Where(entry => entry.Key.StartsWith('$') && entry.Key.TrimStart('$', '.').Length > 0 && entry.Value?.Errors.Count > 0)
                .Select(entry => entry.Key.TrimStart('$', '.'))
                .ToArray();

            if (unknown.Length > 0)
            {
                var errors = unknown.ToDictionary(name => name, name => new[] { $"'{name}' is not a valid field." });
                return BadRequest(ApiResponse<SearchResultsDto>.ValidationFailed(errors, "Please check the fields and try again.") with { TraceId = HttpContext.TraceIdentifier });
            }
        }

        if (request is null)
        {
            return BadRequest(ApiResponse<SearchResultsDto>.BadRequest("A search request body is required."));
        }

        if (!string.IsNullOrWhiteSpace(request.Culture))
        {
            var supported = CmsConstants.Cultures.Resolve(request.Culture.Trim());
            if (supported is null)
            {
                return BadRequest(ApiResponse<SearchResultsDto>.BadRequest("Culture not supported."));
            }

            request = request with { Culture = supported };
        }

        var results = searchService.Search(request);
        return Ok(ApiResponse<SearchResultsDto>.Ok(results, "Search completed successfully."));
    }
}
