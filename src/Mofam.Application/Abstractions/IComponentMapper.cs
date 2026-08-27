using Umbraco.Cms.Core.Models.PublishedContent;
using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Abstractions;

public interface IComponentMapper
{
    IReadOnlyList<ComponentDto> MapComponents(IPublishedProperty? componentsProperty, string? culture);
}
