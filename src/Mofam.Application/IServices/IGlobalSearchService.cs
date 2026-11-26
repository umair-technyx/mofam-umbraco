using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;

namespace Mofam.Application.IServices;

public interface IGlobalSearchService
{
    GlobalSearchDto GetGlobalSearch(GlobalSearchRequest request);
}
