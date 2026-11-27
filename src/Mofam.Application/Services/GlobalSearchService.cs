using System.Globalization;
using Examine;
using Examine.Search;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Infrastructure.Examine;

namespace Mofam.Application.Services;

public sealed class GlobalSearchService(
    IExamineManager examineManager,
    IPublishedUrlProvider urlProvider,
    IPropertyValueMapper valueMapper,
    IPublishedContentQuery contentQuery,
    IFilterService filterService) : IGlobalSearchService
{
    private static readonly string[] ServiceTypes = [CmsConstants.ContentTypes.Service];

    private static readonly string[] OtherTypes =
        [.. GlobalSearchConstants.AllowedContentTypes.Where(t => !string.Equals(t, CmsConstants.ContentTypes.Service, StringComparison.OrdinalIgnoreCase))];

    private sealed record Hit(IPublishedContent Node, string Title);

    #region Global search (typing and "See all")

    // Not cached: the result depends on the text.
    // An error is not caught here: GlobalExceptionMiddleware logs it.
    public GlobalSearchDto GetGlobalSearch(GlobalSearchRequest request)
    {
        // The controller already checked the culture.
        var culture = request.Culture ?? CmsConstants.Cultures.English;

        var term = Normalize(request.Query);

        // Typing: only the suggestions.
        if (request.IsSuggestion == true) return Suggestions(term, culture);

        // Otherwise "See all". No page size means 10.
        var pageSize = request.PageSize is > 0 ? request.PageSize.Value : GlobalSearchConstants.DefaultPageSize;
        var newest = string.Equals(request.SortBy, GlobalSearchConstants.SortNewest, StringComparison.OrdinalIgnoreCase);

        return SeeAll(term, culture, request.PageNumber ?? 1, pageSize, newest, request.Filters);
    }

    // The titles of the best matches. Empty when the text is too short.
    private GlobalSearchDto Suggestions(string term, string culture)
    {
        if (term.Length < GlobalSearchConstants.MinQueryLength) return new GlobalSearchDto();

        var results = Match(GlobalSearchConstants.AllowedContentTypes, term, culture)
            ?.Execute(new QueryOptions(0, GlobalSearchConstants.SuggestedSearchCount));

        return new GlobalSearchDto { SuggestedSearches = results is null ? null : SuggestedSearches(Hits(results, culture)) };
    }

    // One page of results: services first, then the others.
    private GlobalSearchDto SeeAll(string? term, string culture, int pageNumber, int pageSize, bool newest, Dictionary<string, string[]>? filters)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, SearchConstants.MinPageSize, SearchConstants.MaxPageSize);

        long total = 0;
        var items = new List<SearchResultItemDto>();
        List<CategoryCountDto>? categoryCounts = null;

        var categories = PickedCategories(filters);
        var skip = (long)(pageNumber - 1) * pageSize;
        if (term is { Length: >= GlobalSearchConstants.MinQueryLength } && skip <= int.MaxValue)
        {
            // Count first, so the total is right even past the last page.
            var serviceTotal = Count(ServiceTypes, term, culture, categories);
            var otherTotal = Count(OtherTypes, term, culture, categories);
            total = serviceTotal + otherTotal;
            categoryCounts = CategoryCounts(term, culture);

            // Take the services first, then fill the rest of the page with the others.
            var serviceTake = (int)Math.Clamp(serviceTotal - skip, 0, pageSize);
            if (serviceTake > 0)
            {
                var services = Page(Match(ServiceTypes, term, culture, categories), newest, (int)skip, serviceTake);
                if (services is not null) items.AddRange(Hits(services, culture).Select(h => ToItem(h, culture)));
            }

            var otherSkip = Math.Max(0, skip - serviceTotal);
            var otherTake = pageSize - serviceTake;
            if (otherTake > 0 && otherSkip < otherTotal)
            {
                var others = Page(Match(OtherTypes, term, culture, categories), newest, (int)otherSkip, otherTake);
                if (others is not null) items.AddRange(Hits(others, culture).Select(h => ToItem(h, culture)));
            }
        }

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        return new GlobalSearchDto
        {
            Items = items,
            TotalResults = total,
            CategoryCounts = categoryCounts,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPreviousPage = pageNumber > 1,
            HasNextPage = pageNumber < totalPages,
        };
    }

    // The items of the given types that match the words and, when given, are in one of the categories.
    private IBooleanOperation? Match(string[] types, string term, string culture, string[]? categories = null)
    {
        if (!examineManager.TryGetIndex(SearchConstants.IndexName, out var index)) return null;

        var query = index.Searcher.CreateQuery(IndexTypes.Content)
            .GroupedOr([SearchConstants.NodeTypeAliasField], types)
            .And().Field($"{SearchConstants.PublishedField}_{culture}", "y")
            // Leave out items with "Hide from search" on.
            .AndNot(q => q.GroupedOr(CommonHelper.ExpandForCulture([GlobalSearchConstants.HideFromSearchAlias], culture), "1"));

        // Every word must match the start of a word in the title or the description.
        var fields = CommonHelper.ExpandForCulture(GlobalSearchConstants.SearchableFields, culture);
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searched = words.Where(w => !GlobalSearchConstants.StopWords.Contains(w)).ToArray();
        foreach (var word in searched.Length == 0 ? words : searched)
        {
            query = query.And().GroupedOr(fields, word.MultipleCharacterWildcard());
        }

        if (categories is null) return query;

        // Categories were asked for but none is a valid GUID: nothing can match.
        if (categories.Length == 0) return null;

        return query.And().GroupedOr(CommonHelper.ExpandForCulture([CmsConstants.Fields.Categories], culture), categories);
    }

    // The categories of filters.categories as the index keeps them (a GUID without dashes).
    // Null when no category is asked for. An empty array when some are asked for but none is a valid GUID.
    private static string[]? PickedCategories(Dictionary<string, string[]>? filters)
    {
        var values = filters?
            .Where(f => string.Equals(f.Key?.Trim(), CmsConstants.Fields.Categories, StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.Value ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Take(GlobalSearchConstants.MaxFilterValues)
            .ToArray();

        if (values is null or { Length: 0 }) return null;

        return [.. values.Select(v => Guid.TryParse(v.Trim(), out var guid) ? guid.ToString("N") : null).OfType<string>().Distinct()];
    }

    // Lower case words made of letters and digits. Other marks (* ? + - : ( ) ") become spaces,
    // so they cannot break the query.
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var chars = text.Select(c => char.IsLetterOrDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark ? char.ToLowerInvariant(c) : ' ');
        return string.Join(' ', new string([.. chars]).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // How many results each category has. The category filter itself is left out,
    // so the other categories still show their counts.
    private List<CategoryCountDto>? CategoryCounts(string term, string culture)
    {
        var options = filterService.GetFilterData(CmsConstants.ContentTypes.Service, culture)?.Filters
            .FirstOrDefault(f => string.Equals(f.Key, CmsConstants.Fields.Categories, StringComparison.OrdinalIgnoreCase))?.Values;
        if (options is null or { Count: 0 }) return null;

        return [.. options.Select(o => new CategoryCountDto
        {
            Key = o.Key,
            Title = o.Value,
            Count = Guid.TryParse(o.Key, out var guid) ? Count(GlobalSearchConstants.AllowedContentTypes, term, culture, [guid.ToString("N")]) : 0,
        })];
    }

    // A slice of the results: best match first, or newest first.
    private static ISearchResults? Page(IBooleanOperation? query, bool newest, int skip, int take)
    {
        if (query is null) return null;

        var options = new QueryOptions(skip, take);
        return newest
            ? query.OrderByDescending(new SortableField(GlobalSearchConstants.UpdateDateField, SortType.Long)).Execute(options)
            : query.Execute(options);
    }

    // How many items match.
    private long Count(string[] types, string term, string culture, string[]? categories = null) =>
        Match(types, term, culture, categories)?.Execute(new QueryOptions(0, 1)).TotalItemCount ?? 0;

    // The found nodes with their title (a page without a title uses its name).
    private List<Hit> Hits(ISearchResults results, string culture)
    {
        var hits = new List<Hit>();
        foreach (var result in results)
        {
            if (!int.TryParse(result.Id, out var id) || contentQuery.Content(id) is not { } node) continue;

            var title = Blank(valueMapper.Text(node, CmsConstants.Fields.Title, culture)) ?? Blank(NodeName(node, culture));
            if (title is not null) hits.Add(new Hit(node, title));
        }

        return hits;
    }

    // The titles in lower case, without repeats.
    private static List<string>? SuggestedSearches(List<Hit> best)
    {
        var phrases = best
            .Select(h => h.Title.Trim().ToLowerInvariant())
            .Distinct()
            .Take(GlobalSearchConstants.SuggestedSearchCount)
            .ToList();

        return phrases.Count == 0 ? null : phrases;
    }

    private SearchResultItemDto ToItem(Hit hit, string culture)
    {
        var url = urlProvider.GetUrl(hit.Node, UrlMode.Default, culture);

        // The first category, if any.
        var category = valueMapper.Raw(hit.Node, CmsConstants.Fields.Categories, culture) is IEnumerable<IPublishedContent> categories
            ? Blank(categories.Select(c => valueMapper.Text(c, CmsConstants.Fields.Title, culture)).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)))
            : null;

        return new SearchResultItemDto
        {
            Title = hit.Title,
            Description = Blank(valueMapper.Text(hit.Node, CmsConstants.Fields.Description, culture)),
            PageTag = PageTag(hit.Node, culture),
            Category = category,
            Slug = Blank(valueMapper.Text(hit.Node, CmsConstants.Fields.Slug, culture)),

            // No CTA when "Hide CTA" is on for the node (Card Settings), or when it has no URL (Umbraco gives "#").
            Cta = HidesCta(hit.Node, culture) || string.IsNullOrWhiteSpace(url) || url == "#"
                ? null
                : new LinkDto { Url = url, Name = hit.Title },
        };
    }

    #endregion

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    // True when "Hide CTA" is turned on for the node. A type without the field shows the CTA.
    private bool HidesCta(IPublishedContent node, string culture) =>
        valueMapper.Raw(node, CmsConstants.ThumbnailCards.HideCta, culture) is true;

    // The title of the Page Tag picked (the first one), or null.
    private string? PageTag(IPublishedContent node, string culture)
    {
        var tag = valueMapper.Raw(node, GlobalSearchConstants.PageTagAlias, culture) switch
        {
            IPublishedContent one => one,
            IEnumerable<IPublishedContent> many => many.FirstOrDefault(),
            _ => null,
        };

        return tag is null
            ? null
            : Blank(valueMapper.Text(tag, CmsConstants.Fields.Title, culture)) ?? Blank(NodeName(tag, culture));
    }

    private static string NodeName(IPublishedContent node, string culture) =>
        node.Cultures.TryGetValue(culture, out var info) ? info.Name : node.Name;
}
