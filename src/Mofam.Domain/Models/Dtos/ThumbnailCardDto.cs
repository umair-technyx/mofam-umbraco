namespace Mofam.Domain.Models.Dtos;

/// <summary>
/// One card of the thumbnail cards section. It is built when the page is requested, from the node the editor
/// picked in the section's link picker (a service, a page...), so the front end gets the card text and the link
/// in one place and does not need a second call.
/// </summary>
public sealed record ThumbnailCardDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }

    /// <summary>The background shape of the card (a service's <c>backgroundDecoration</c>), when the node has one.</summary>
    public string? Shape { get; init; }

    /// <summary>The tags of the node (a service's tags), when it has any. Same shape as on the service itself.</summary>
    public object? Tags { get; init; }

    /// <summary>The "Start Service" / "Learn more" link: the page link, with the title the editor typed in the picker.</summary>
    public LinkDto? Cta { get; init; }
}
