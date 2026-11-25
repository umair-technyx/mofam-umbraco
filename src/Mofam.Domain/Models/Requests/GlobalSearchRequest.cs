using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Mofam.Domain.Constants;

namespace Mofam.Domain.Models.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GlobalSearchRequest
{
    /// <summary>The text typed. Shorter than <see cref="GlobalSearchConstants.MinQueryLength"/> gives an empty answer.</summary>
    [StringLength(SearchConstants.MaxQueryLength, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    public string? Query { get; init; }

    public string? Culture { get; init; }

    /// <summary>true: answer only the suggested searches (while typing). false or empty: "See all".</summary>
    public bool? IsSuggestion { get; init; }

    /// <summary>How many results on a page of "See all". 10 when empty.</summary>
    public int? PageSize { get; init; }

    /// <summary>Only "categories" is used: the keys (GUIDs) of the picked categories. An item can be in any one of them.</summary>
    public Dictionary<string, string[]>? Filters { get; init; }

    /// <summary>"relevance" (default) or "newest" (last updated first). Used only on "See all".</summary>
    [RegularExpression($"^(?i)({GlobalSearchConstants.SortRelevance}|{GlobalSearchConstants.SortNewest})$", ErrorMessage = "SortBy must be relevance or newest.")]
    public string? SortBy { get; init; }

    /// <summary>The page to return (1 when empty). Used only on "See all".</summary>
    public int? PageNumber { get; init; }
}
