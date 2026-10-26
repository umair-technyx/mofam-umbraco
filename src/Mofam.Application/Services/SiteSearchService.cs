using Examine;
using Examine.Search;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Domain.Constants;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Examine;
using Mofam.Application.IServices;

namespace Mofam.Application.Services;

public sealed class SiteSearchService(
    IExamineManager examineManager,
    IPublishedContentQuery contentQuery,
    IPageMapper pageMapper,
    ILogger logger) : ISiteSearchService
{
    public SearchResultsDto Search(SearchRequest request)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, SearchConstants.MinPageSize, SearchConstants.MaxPageSize);

        var skipLong = (long)(pageNumber - 1) * pageSize;
        if (skipLong > int.MaxValue)
        {
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }

        if (!examineManager.TryGetIndex(SearchConstants.IndexName, out var index))
        {
            logger.Warning("Examine index {IndexName} is not available — returning no results", SearchConstants.IndexName);
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }

        var contentTypes = ResolveContentTypes(request);
        if (contentTypes is null)
        {
            // Caller asked for content types that are not on the allow-list.
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }

        try
        {
            var query = BuildQuery(index.Searcher, request, contentTypes);
            if (query is null)
            {
                // A filter had only values that are not GUIDs, so nothing can match it.
                return SearchResultsDto.Empty(pageNumber, pageSize);
            }

            // Paging happens in Lucene, not in memory — the whole point of using the index.
            var skip = (int)skipLong;
            var results = query.Execute(new QueryOptions(skip, pageSize));

            var total = results.TotalItemCount;
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            return new SearchResultsDto
            {
                Items = results.Select(r => MapHit(r, request.Culture)).Where(h => h is not null).Select(h => h!).ToList(),
                TotalResults = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Search failed. Query={Query}, ContentType={ContentType}, Culture={Culture}",
                request.Query, request.ContentType, request.Culture);
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }
    }

    /// <summary>
    /// The content types to search. A single requested type is honoured when it is on the
    /// allow-list; null means the caller asked for something it may not reach, and an
    /// omitted type falls back to everything allowed.
    /// </summary>
    private static string[]? ResolveContentTypes(SearchRequest request)
    {
        var requested = request.ContentType?.Trim();

        if (string.IsNullOrEmpty(requested))
        {
            return SearchConstants.AllowedContentTypes;
        }

        if (SearchConstants.AllowedContentTypes.Length == 0)
        {
            return [requested];
        }

        return SearchConstants.AllowedContentTypes.Contains(requested, StringComparer.OrdinalIgnoreCase)
            ? [requested]
            : null;
    }

    private IBooleanOperation? BuildQuery(
        ISearcher searcher,
        SearchRequest request,
        string[] contentTypes)
    {
        var query = searcher.CreateQuery(IndexTypes.Content);

        // Culture-variant content is flagged per culture; invariant content uses the bare field.
        var publishedField = string.IsNullOrWhiteSpace(request.Culture)
            ? SearchConstants.PublishedField
            : $"{SearchConstants.PublishedField}_{request.Culture}";

        IBooleanOperation op = contentTypes.Length > 0
            ? query.GroupedOr([SearchConstants.NodeTypeAliasField], contentTypes)
            : query.Field(publishedField, "y");

        if (contentTypes.Length > 0)
        {
            op = op.And().Field(publishedField, "y");
        }

        if (!string.IsNullOrWhiteSpace(request.Query) && SearchConstants.SearchableFields.Length > 0)
        {
            // * and ? are Lucene wildcards: a leading one is a parse error, so they are not part of the search text.
            var term = request.Query.Replace("*", "").Replace("?", "").Trim().ToLowerInvariant();
            if (term.Length == 0) return null;

            op = op.And().GroupedOr(CommonHelper.ExpandForCulture(SearchConstants.SearchableFields, request.Culture),
                                    term.MultipleCharacterWildcard());
        }

        if (request.Filters is { Count: > 0 })
        {
            foreach (var (field, values) in request.Filters)
            {
                var clean = values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray() ?? [];
                if (string.IsNullOrWhiteSpace(field) || clean.Length == 0) continue;

                var guids = FilterValues(clean);
                if (guids.Length == 0) return null;

                op = op.And().GroupedOr(CommonHelper.ExpandForCulture([field], request.Culture), guids);
            }
        }

        return op;
    }

    /// <summary>
    /// The filters endpoint hands out node keys as plain GUIDs, but Examine stores picker
    /// values as UDIs — <c>umb://document/6395c1e5792e407f804a28bdd3972439</c>. The index splits a
    /// UDI into the tokens "umb", "document" and the dash-less GUID, so a GUID is queried only as that
    /// dash-less token. Querying the UDI or the dashed GUID would be split too, and "umb" and "document"
    /// are in every item, so the filter would match everything.
    /// Values that are not a GUID are dropped for the same reason (for example "document" would match everything).
    /// </summary>
    private static string[] FilterValues(string[] values)
    {
        return [.. values
            .Select(v => Guid.TryParse(v?.Trim(), out var guid) ? guid.ToString("N") : null)
            .OfType<string>()
            .Distinct()];
    }


    /// <summary>
    /// Resolves the indexed hit back to published content and maps it in listing mode —
    /// the same model the detail endpoint returns, minus detailPageComponents and SEO.
    /// </summary>
    private PageDto? MapHit(ISearchResult result, string? culture)
    {
        if (!int.TryParse(result.Id, out var nodeId)) return null;

        var content = contentQuery.Content(nodeId);
        return content is null ? null : pageMapper.Map(content, culture, PageMapMode.Listing);
    }
}
