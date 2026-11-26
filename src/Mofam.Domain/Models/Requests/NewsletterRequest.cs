using System.ComponentModel.DataAnnotations;
using Mofam.Domain.Attributes;
using Mofam.Domain.Constants;

namespace Mofam.Domain.Models.Requests;

public sealed record NewsletterRequest
{
    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [EmailAddress(ErrorMessage = GeneralConstants.FieldInvalidMessage)]
    [RegularExpression(GeneralConstants.EmailPattern, ErrorMessage = GeneralConstants.FieldInvalidMessage)]
    [SafeText]
    public string? Email { get; init; }

    public string? RecaptchaToken { get; init; }

    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    public string? Website { get; init; }
}
