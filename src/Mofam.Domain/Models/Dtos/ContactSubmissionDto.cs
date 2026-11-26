using System.Globalization;
using Mofam.Domain.Constants;

namespace Mofam.Domain.Models.Dtos;

public sealed class ContactSubmissionDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool EmailSent { get; set; }
    public string?[] ToCsvRow() =>
    [
        Id.ToString(CultureInfo.InvariantCulture),
        CreatedAt.ToString(ExportConstants.DateFormat, CultureInfo.InvariantCulture),
        Subject, FirstName, LastName, Email, ContactNumber, Message, Culture,
        EmailSent ? ExportConstants.Yes : ExportConstants.No,
    ];
}
