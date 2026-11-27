using System.Globalization;
using Mofam.Domain.Constants;

namespace Mofam.Domain.Models.Dtos;

// AlreadySubscribed is true when the email was on the list before: nothing is saved or sent again.
// The request id is sent as the traceId of the response, not here.
public sealed record NewsletterReceiptDto(bool AlreadySubscribed = false);
public sealed class NewsletterSubscriberDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool EmailSent { get; set; }
    public string?[] ToCsvRow() =>
    [
        Id.ToString(CultureInfo.InvariantCulture),
        CreatedAt.ToString(ExportConstants.DateFormat, CultureInfo.InvariantCulture),
        Email, Culture,
        EmailSent ? ExportConstants.Yes : ExportConstants.No,
    ];
}

public sealed class NewsletterSaveResult
{
    public int Id { get; set; }
    public bool IsNew { get; set; }
}
