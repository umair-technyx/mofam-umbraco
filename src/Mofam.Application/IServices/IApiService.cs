using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.IServices;

public interface IApiService
{
    PageDto? GetPageBySlug(string contentTypeAlias, string slug, string? culture);
}
