using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

// Finds a channel root node (web, app, etc) by its content type alias.
// Roots rarely change, so the resolved keys are cached.
public interface ISiteRootResolver
{
    #region Root

    // Returns the root node for the alias, or null if it does not exist or is unpublished.
    IPublishedContent? GetRoot(string rootAlias);

    #endregion
}
