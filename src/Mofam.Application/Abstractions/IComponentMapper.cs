using Umbraco.Cms.Core.Models.PublishedContent;
using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.Abstractions;

/// <summary>
/// Flattens authored Block List/Grid elements into <see cref="ComponentDto"/>s.
/// <para>
/// This mapper owns element/block shaping only. It has no notion of
/// <see cref="Mofam.Application.Abstractions.PageMapMode"/> or <see cref="PageDto"/> — when
/// recursion meets a genuine <see cref="IPublishedContent"/> (i.e. a real page reached
/// through a picker, not an authored element), it hands the node to the caller-supplied
/// <paramref name="resolvePage"/> callback rather than deciding for itself how to shape it.
/// This keeps the dependency one-directional: <c>PageMapper</c> depends on this interface
/// (for the ordered <c>components</c>/<c>detailPageComponents</c> list), so this mapper must
/// never depend back on <c>IPageMapper</c> — that would be a constructor-injection cycle.
/// </para>
/// </summary>
public interface IComponentMapper
{
    /// <summary>
    /// Maps one components/detail-components property.
    /// </summary>
    /// <param name="componentsProperty">The property holding the Block List/Grid or picker value.</param>
    /// <param name="culture">Requested culture.</param>
    /// <param name="resolvePage">
    /// Called whenever recursion meets a real content page (as opposed to an authored
    /// element) — maps it in listing shape and returns the resulting <see cref="PageDto"/>.
    /// </param>
    /// <param name="ancestors">
    /// Keys of nodes already on the current branch, for cycle detection. One set is
    /// threaded through the whole page build (see <c>PageMapper</c>) — this method must
    /// treat it as belonging to the caller: copy before extending, never mutate in place.
    /// </param>
    IReadOnlyList<ComponentDto> MapComponents(
        IPublishedProperty? componentsProperty,
        string? culture,
        Func<IPublishedContent, ISet<Guid>, PageDto> resolvePage,
        ISet<Guid> ancestors);
}
