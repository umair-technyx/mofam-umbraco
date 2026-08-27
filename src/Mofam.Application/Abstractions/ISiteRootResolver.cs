using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

/// <summary>
/// Resolves a channel root node (web, app, …) by its content type alias.
/// Roots change very rarely, so resolved keys are cached.
/// </summary>
public interface ISiteRootResolver
{
    /// <summary>
    /// Returns the root node for <paramref name="rootAlias"/>, or null when it does not exist
    /// or is unpublished.
    /// </summary>
    IPublishedContent? GetRoot(string rootAlias);
}
