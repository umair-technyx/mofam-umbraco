using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;

namespace Mofam.Application.IServices;

/// <summary>
/// Examine-backed content search. Filtering, paging and sorting are pushed into the
/// index rather than done in memory, so cost does not grow with the size of the site.
/// </summary>
public interface ISiteSearchService
{
    SearchResultsDto Search(SearchRequest request);
}
