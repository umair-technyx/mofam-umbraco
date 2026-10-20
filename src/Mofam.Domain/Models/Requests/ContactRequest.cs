using System.ComponentModel.DataAnnotations;
using Mofam.Domain.Attributes;
using Mofam.Domain.Constants;

namespace Mofam.Domain.Models.Requests;

public sealed record ContactRequest
{
    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [SafeText]
    public string? Subject { get; init; }

    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [SafeText]
    public string? FirstName { get; init; }

    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [SafeText]
    public string? LastName { get; init; }

    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [EmailAddress(ErrorMessage = GeneralConstants.FieldInvalidMessage)]
    [RegularExpression(GeneralConstants.EmailPattern, ErrorMessage = GeneralConstants.FieldInvalidMessage)]
    [SafeText]
    public string? Email { get; init; }

    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [Phone(ErrorMessage = GeneralConstants.FieldInvalidMessage)]
    [RegularExpression(GeneralConstants.PhonePattern, ErrorMessage = GeneralConstants.FieldInvalidMessage)]
    [SafeText]
    public string? ContactNumber { get; init; }

    [Required(ErrorMessage = GeneralConstants.FieldRequiredMessage)]
    [StringLength(3000, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    [SafeText(AllowLineBreaks = true)]
    public string? Message { get; init; }

    public string? RecaptchaToken { get; init; }

    // A hidden field. Real visitors never fill it, bots often do.
    [StringLength(255, ErrorMessage = GeneralConstants.FieldTooLongMessage)]
    public string? Website { get; init; }
}
