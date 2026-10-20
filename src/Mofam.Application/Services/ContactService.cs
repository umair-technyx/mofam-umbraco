using System.Net;
using Mofam.Application.Abstractions;
using Mofam.Application.IServices;
using Mofam.Application.Helpers;
using Mofam.Domain.Attributes;
using Mofam.Domain.Constants;
using Mofam.Domain.Exceptions;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using static Mofam.Domain.Constants.GeneralConstants;

namespace Mofam.Application.Services;

public sealed class ContactService(
    IContactEmailConfigMapper configMapper,
    EmailHelper emailHelper,
    IRecaptchaService recaptchaService,
    Serilog.ILogger logger) : IContactService
{
   
    public async Task<ContactReceiptDto> SubmitAsync(ContactRequest request, string culture, string? clientIp, string reference, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleAsync(request, culture, clientIp, reference, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not HttpStatusException)
        {
            logger.Error(ex, "Contact submission {Reference} failed unexpectedly. Culture={Culture}", reference, culture);
            throw Failed();
        }
    }

    #region Private Methods
    private async Task<ContactReceiptDto> HandleAsync(ContactRequest request, string culture, string? clientIp, string reference, CancellationToken cancellationToken)
    {
        #region Check the request

        // Hidden field filled means bot. Return fake success; send nothing.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            logger.Information("Contact submission {Reference} was dropped (hidden field filled). Culture={Culture}", reference, culture);
            return new ContactReceiptDto(reference);
        }

        var data = request with
        {
            Subject = Clean(request.Subject),
            FirstName = Clean(request.FirstName),
            LastName = Clean(request.LastName),
            Email = request.Email?.Trim(),
            ContactNumber = Clean(request.ContactNumber),
            Message = Clean(request.Message),
            RecaptchaToken = request.RecaptchaToken?.Trim(),
            Website = null,
        };

        var oneLine = new[] { data.Subject, data.FirstName, data.LastName, data.Email, data.ContactNumber };

        if (oneLine.Any(CommonHelper.IsBlank) || CommonHelper.IsBlank(data.Message) || !CommonHelper.IsValidEmail(data.Email)
            || oneLine.Any(text => !new SafeTextAttribute().IsValid(text))
            || !new SafeTextAttribute { AllowLineBreaks = true }.IsValid(data.Message))
        {
            throw new HttpStatusException(HttpStatusCode.BadRequest, "Please check the fields and try again.");
        }

        #endregion

        #region reCAPTCHA

        var captcha = await recaptchaService.VerifyAsync(data.RecaptchaToken, clientIp, cancellationToken);

        switch (captcha.Status)
        {
            case RecaptchaStatus.Failed:
                logger.Information(
                    "Contact submission {Reference} failed the security check. Reason={Reason}, Score={Score}",
                    reference, captcha.Reason, captcha.Score);

                throw new HttpStatusException(
                    HttpStatusCode.BadRequest,
                    "The security check failed. Please try again.",
                    new Dictionary<string, string[]> { ["recaptchaToken"] = [string.Format(GeneralConstants.FieldInvalidMessage, "recaptchaToken")] });

            case RecaptchaStatus.Unavailable:
                logger.Error("Contact submission {Reference} could not be checked by reCAPTCHA. Reason={Reason}", reference, captcha.Reason);
                throw Unavailable();
        }

        #endregion

        return await SendAsync(data, culture, reference);
    }

    private async Task<ContactReceiptDto> SendAsync(ContactRequest data, string culture, string reference)
    {
        #region Content and sender

        var config = configMapper.TryGet(culture, out var failure);
        if (config is null)
        {
            logger.Error("Contact submission {Reference} cannot be sent. Culture={Culture}. {Reason}", reference, culture, failure);
            throw Unavailable();
        }

        var subject = config.AdminSubject ?? string.Empty;

        var body = config.AdminBody
            .Replace("{{Subject}}", WebUtility.HtmlEncode(data.Subject ?? string.Empty))
            .Replace("{{FirstName}}", WebUtility.HtmlEncode(data.FirstName ?? string.Empty))
            .Replace("{{LastName}}", WebUtility.HtmlEncode(data.LastName ?? string.Empty))
            .Replace("{{Email}}", WebUtility.HtmlEncode(data.Email ?? string.Empty))
            .Replace("{{ContactNumber}}", WebUtility.HtmlEncode(data.ContactNumber ?? string.Empty))
            .Replace("{{Message}}", WebUtility.HtmlEncode(data.Message ?? string.Empty).Replace("\n", "<br>"));

        #endregion

        await emailHelper.SendAsync(config.AdminEmail, data.Email, subject, body, null, reference, CancellationToken.None);

        logger.Information("Contact submission {Reference} was sent. Culture={Culture}", reference, culture);

        return new ContactReceiptDto(reference);
    }

    private static string Clean(string? text) => CommonHelper.StripHtml(text).Trim();

    private static HttpStatusException Unavailable() => new(HttpStatusCode.ServiceUnavailable, GeneralConstants.ServiceUnavailableMessage);

    private static HttpStatusException Failed() => new(HttpStatusCode.InternalServerError, GeneralConstants.UnexpectedErrorMessage);

    #endregion
}
