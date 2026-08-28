using Examine;
using Examine.Search;
using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Mofam.Domain.Options;
using Serilog;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Infrastructure.Examine;

namespace Mofam.Application.Services;

public sealed class ContentSearchService(
    IExamineManager examineManager,
    IPublishedContentQuery contentQuery,
    IMediaUrlBuilder mediaUrlBuilder,
    IOptions<SearchOptions> searchOptions,
    ILogger logger) : IContentSearchService
{
    private const string NodeTypeAliasField = "__NodeTypeAlias";
    private const string PublishedField = "__Published";

    public SearchResultsDto Search(SearchRequest request)
    {
        var options = searchOptions.Value;

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, Math.Max(1, options.MaxPageSize));

        if (!examineManager.TryGetIndex(options.IndexName, out var index))
        {
            logger.Warning("Examine index {IndexName} is not available — returning no results", options.IndexName);
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }

        var contentTypes = ResolveContentTypes(request, options);
        if (contentTypes is null)
        {
            // Caller asked for content types that are not on the allow-list.
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }

        try
        {
            var query = BuildQuery(index.Searcher, request, contentTypes, options);

            // Paging happens in Lucene, not in memory — the whole point of using the index.
            var skip = (pageNumber - 1) * pageSize;
            var results = query.Execute(new QueryOptions(skip, pageSize));

            var total = results.TotalItemCount;
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            return new SearchResultsDto
            {
                Items = results.Select(r => MapHit(r, request.Culture, options)).Where(h => h is not null).Select(h => h!).ToList(),
                TotalResults = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Search failed. Query={Query}, ContentTypes={ContentTypes}, Culture={Culture}",
                request.Query, string.Join(",", contentTypes), request.Culture);
            return SearchResultsDto.Empty(pageNumber, pageSize);
        }
    }

    /// <summary>
    /// Returns the content types to search, or null when the caller asked for something
    /// outside the configured allow-list.
    /// </summary>
    private static string[]? ResolveContentTypes(SearchRequest request, SearchOptions options)
    {
        var requested = request.ContentTypes?
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (options.AllowedContentTypes.Length == 0)
        {
            return requested;
        }

        if (requested.Length == 0)
        {
            return options.AllowedContentTypes;
        }

        var disallowed = requested
            .Where(t => !options.AllowedContentTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return disallowed.Length > 0 ? null : requested;
    }

    private IQueryExecutor BuildQuery(
        ISearcher searcher,
        SearchRequest request,
        string[] contentTypes,
        SearchOptions options)
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

        if (!string.IsNullOrWhiteSpace(request.Query) && options.SearchableFields.Length > 0)
        {
            var term = request.Query.Trim().ToLowerInvariant();
            op = op.And().GroupedOr(options.SearchableFields, term.MultipleCharacterWildcard());
        }

        if (request.Filters is { Count: > 0 })
        {
            foreach (var (field, values) in request.Filters)
            {
                var clean = values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray() ?? [];
                if (string.IsNullOrWhiteSpace(field) || clean.Length == 0) continue;

                op = op.And().GroupedOr([field], clean);
            }
        }

        return ApplySort(op, request.SortBy);
    }

    // OrderBy/OrderByDescending return IOrdering, so the shared type here is IQueryExecutor.
    private static IQueryExecutor ApplySort(IBooleanOperation op, SearchSortBy sortBy) => sortBy switch
    {
        SearchSortBy.NewestFirst => op.OrderByDescending(new SortableField("updateDate", SortType.Long)),
        SearchSortBy.OldestFirst => op.OrderBy(new SortableField("updateDate", SortType.Long)),
        SearchSortBy.NameAscending => op.OrderBy(new SortableField("nodeName", SortType.String)),
        _ => op, // Relevance is Lucene's default ordering.
    };

    /// <summary>
    /// Resolves the indexed hit back to published content so the response carries real
    /// values rather than whatever happened to be indexed.
    /// </summary>
    private SearchHitDto? MapHit(ISearchResult result, string? culture, SearchOptions options)
    {
        if (!int.TryParse(result.Id, out var nodeId)) return null;

        var content = contentQuery.Content(nodeId);
        if (content is null) return null;

        content.Cultures.TryGetValue(culture ?? string.Empty, out var cultureInfo);

        return new SearchHitDto
        {
            Id = content.Key.ToString(),
            ContentType = content.ContentType.Alias,
            Name = cultureInfo?.Name ?? content.Name ?? string.Empty,
            Slug = cultureInfo?.UrlSegment,
            Summary = result.Values.TryGetValue("description", out var description) ? description : null,
            Image = ResolveImage(content, culture, options),
            Score = result.Score,
        };
    }

    /// <summary>
    /// Attaches the first configured image property that actually resolves, so a hit can
    /// be rendered as a card without a follow-up request.
    /// </summary>
    private MediaDto? ResolveImage(IPublishedContent content, string? culture, SearchOptions options)
    {
        foreach (var alias in options.ImageFieldAliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;

            var value = content.GetProperty(alias)?.GetValue(culture);

            var image = value switch
            {
                IEnumerable<IPublishedContent> many => mediaUrlBuilder.BuildMany(many, culture).FirstOrDefault(),
                IPublishedContent one => mediaUrlBuilder.Build(one, culture),
                _ => null,
            };

            if (image is not null) return image;
        }

        return null;
    }
}
