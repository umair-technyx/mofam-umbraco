using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;

namespace Mofam.Application.IServices;

public interface IGlobalSearchService
{
    // Typing (isSuggestion true): the suggestions. Otherwise "See all": one page of results. Never null.
    GlobalSearchDto GetGlobalSearch(GlobalSearchRequest request);
}
