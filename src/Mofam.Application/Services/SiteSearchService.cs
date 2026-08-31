using Examine;
using Examine.Search;
using Mofam.Application.Abstractions;
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
    private const string NodeTypeAliasField = "__NodeTypeAlias";
    private const string PublishedField = "__Published";

    public SearchResultsDto Search(SearchRequest request)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = request.PageSize;

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

            // Paging happens in Lucene, not in memory — the whole point of using the index.
            var skip = (pageNumber - 1) * pageSize;
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

    private IBooleanOperation BuildQuery(
        ISearcher searcher,
        SearchRequest request,
        string[] contentTypes)
    {
        var query = searcher.CreateQuery(IndexTypes.Content);

        // Culture-variant content is flagged per culture; invariant content uses the bare field.
        var publishedField = string.IsNullOrWhiteSpace(request.Culture)
            ? PublishedField
            : $"{PublishedField}_{request.Culture}";

        IBooleanOperation op = contentTypes.Length > 0
            ? query.GroupedOr([NodeTypeAliasField], contentTypes)
            : query.Field(publishedField, "y");

        if (contentTypes.Length > 0)
        {
            op = op.And().Field(publishedField, "y");
        }

        if (!string.IsNullOrWhiteSpace(request.Query) && SearchConstants.SearchableFields.Length > 0)
        {
            var term = request.Query.Trim().ToLowerInvariant();
            op = op.And().GroupedOr(ExpandForCulture(SearchConstants.SearchableFields, request.Culture),
                                    term.MultipleCharacterWildcard());
        }

        if (request.Filters is { Count: > 0 })
        {
            foreach (var (field, values) in request.Filters)
            {
                var clean = values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray() ?? [];
                if (string.IsNullOrWhiteSpace(field) || clean.Length == 0) continue;

                op = op.And().GroupedOr(ExpandForCulture([field], request.Culture), FilterValues(clean));
            }
        }

        return op;
    }

    /// <summary>
    /// Umbraco indexes a culture-variant property as <c>alias_culture</c> and an invariant
    /// one as plain <c>alias</c>. Which applies depends on how the doctype is configured,
    /// so both names are queried.
    /// </summary>
    private static string[] ExpandForCulture(string[] aliases, string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return aliases;

        var expanded = new List<string>(aliases.Length * 2);

        foreach (var alias in aliases)
        {
            expanded.Add(alias);
            expanded.Add($"{alias}_{culture}");
        }

        return [.. expanded];
    }

    /// <summary>
    /// The filters endpoint hands out node keys as plain GUIDs, but Examine stores picker
    /// values as UDIs — <c>umb://document/6395c1e5792e407f804a28bdd3972439</c>. Both the
    /// UDI and its dash-less GUID token are queried so the round-trip matches.
    /// </summary>
    private static string[] FilterValues(string[] values)
    {
        var result = new List<string>(values.Length * 3);

        foreach (var value in values)
        {
            result.Add(value);

            if (Guid.TryParse(value, out var guid))
            {
                result.Add(new GuidUdi(Umbraco.Cms.Core.Constants.UdiEntityType.Document, guid).ToString());
                result.Add(guid.ToString("N"));
            }
        }

        return [.. result.Distinct(StringComparer.OrdinalIgnoreCase)];
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
