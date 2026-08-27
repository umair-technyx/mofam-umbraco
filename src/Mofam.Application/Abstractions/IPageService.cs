using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Abstractions;

public interface IPageService
{
    PageDto? GetPageBySlug(string rootAlias, string pageContentTypeAlias, string slug, string? culture);
}
