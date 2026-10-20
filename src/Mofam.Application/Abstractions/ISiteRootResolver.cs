using Umbraco.Cms.Core.Models.PublishedContent;

namespace Mofam.Application.Abstractions;

public interface ISiteRootResolver
{
    #region Root

    // Returns the root node for the alias, or null if it does not exist or is unpublished.
    IPublishedContent? GetRoot(string rootAlias);

    // Returns the first published child of the root with this content type alias, or null if there is none.
    IPublishedContent? FindChild(IPublishedContent root, string contentTypeAlias);

    #endregion
}
