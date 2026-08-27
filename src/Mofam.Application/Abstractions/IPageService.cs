using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Abstractions;

public interface IPageService
{
    PageDto? GetPageBySlug(string contentTypeAlias, string slug, string? culture);
}
