using System.Net;
using System.Text.RegularExpressions;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.IServices;
using Mofam.Domain.Attributes;
using Mofam.Domain.Constants;
using Mofam.Domain.Exceptions;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using static Mofam.Domain.Constants.GeneralConstants;

namespace Mofam.Application.Services;

public sealed partial class NewsletterService(
    INewsletterEmailConfigMapper configMapper,
    INewsletterSubscriptionService subscriptionService,
    EmailHelper emailHelper,
    IRecaptchaService recaptchaService,
    Serilog.ILogger logger) : INewsletterService
{
    public async Task<NewsletterReceiptDto> SubscribeAsync(NewsletterRequest request, string culture, string? clientIp, string reference, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleAsync(request, culture, clientIp, reference, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not HttpStatusException)
        {
            logger.Error(ex, "Newsletter subscription {Reference} failed unexpectedly. Culture={Culture}", reference, culture);
            throw Failed();
        }
    }

    #region Private Methods
    private async Task<NewsletterReceiptDto> HandleAsync(NewsletterRequest request, string culture, string? clientIp, string reference, CancellationToken cancellationToken)
    {
        #region Check the request

        // Hidden field filled means bot. Return fake success; save and send nothing.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            logger.Information("Newsletter subscription {Reference} was dropped (hidden field filled). Culture={Culture}", reference, culture);
            return new NewsletterReceiptDto(reference);
        }

        // Lower case, so the same email in other letters is the same subscriber.
        var email = request.Email?.Trim().ToLowerInvariant();

        if (CommonHelper.IsBlank(email) || !CommonHelper.IsValidEmail(email) || !new SafeTextAttribute().IsValid(email))
        {
            throw new HttpStatusException(HttpStatusCode.BadRequest, "Please check the fields and try again.");
        }

        #endregion

        #region reCAPTCHA

        var captcha = await recaptchaService.VerifyAsync(request.RecaptchaToken?.Trim(), clientIp, cancellationToken);

        switch (captcha.Status)
        {
            case RecaptchaStatus.LowScore:
                logger.Information("Newsletter subscription {Reference} was blocked by reCAPTCHA. Score={Score}", reference, captcha.Score);
                throw new HttpStatusException(HttpStatusCode.Forbidden, "The security check failed. Please try again.");

            case RecaptchaStatus.Failed:
                logger.Information(
                    "Newsletter subscription {Reference} failed the security check. Reason={Reason}, Score={Score}",
                    reference, captcha.Reason, captcha.Score);

                throw new HttpStatusException(
                    HttpStatusCode.BadRequest,
                    "The security check failed. Please try again.",
                    new Dictionary<string, string[]> { ["recaptchaToken"] = [string.Format(GeneralConstants.FieldInvalidMessage, "recaptchaToken")] });

            case RecaptchaStatus.Unavailable:
                logger.Error("Newsletter subscription {Reference} could not be checked by reCAPTCHA. Reason={Reason}", reference, captcha.Reason);
                throw Unavailable();

            case RecaptchaStatus.Passed:
            case RecaptchaStatus.Skipped:
                break;

            default:
                logger.Error("Newsletter subscription {Reference} got an unknown reCAPTCHA status {Status}.", reference, captcha.Status);
                throw Unavailable();
        }

        #endregion

        // The email is saved first, so it is kept even when the emails are off or fail.
        var saved = await subscriptionService.SaveAsync(email!, culture);

        if (!saved.IsNew)
        {
            // Already subscribed: the visitor sees the same success, and no email is sent again.
            logger.Information("Newsletter subscription {Reference}: the email was already subscribed. Id={Id}, Culture={Culture}", reference, saved.Id, culture);
            return new NewsletterReceiptDto(reference);
        }

        logger.Information("Newsletter subscription {Reference} was saved. Id={Id}, Culture={Culture}", reference, saved.Id, culture);

        if (!configMapper.IsEmailEnabled(culture))
        {
            logger.Information("Newsletter subscription {Reference} was not emailed: the newsletter email is off in the website settings. Id={Id}", reference, saved.Id);
            return new NewsletterReceiptDto(reference);
        }

        await SendAsync(email!, culture, reference, saved.Id);

        return new NewsletterReceiptDto(reference);
    }

    // The subscriber is already saved here, so a problem with an email is logged and the visitor is not told it failed.
    private async Task SendAsync(string email, string culture, string reference, int id)
    {
        var config = configMapper.TryGet(culture, out var failure);
        if (config is null)
        {
            logger.Error("Newsletter subscription {Reference} was saved (Id={Id}) but cannot be emailed. Culture={Culture}. {Reason}", reference, id, culture, failure);
            return;
        }

        // The email is put in the template in one pass, as plain text (encoded), so it cannot add any HTML.
        var encoded = WebUtility.HtmlEncode(email);
        var adminBody = EmailPlaceholder().Replace(config.AdminBody, _ => encoded);
        var userBody = EmailPlaceholder().Replace(config.UserBody, _ => encoded);

        // The admin gets the news of the subscriber (a reply goes to the subscriber), the subscriber gets the welcome.
        var adminSent = await TrySendAsync(config.AdminEmail, email, config.Subject, adminBody, reference, id, "admin");
        var userSent = await TrySendAsync(email, null, config.Subject, userBody, reference, id, "subscriber");

        if (!adminSent || !userSent) return;

        logger.Information("Newsletter subscription {Reference} was emailed. Id={Id}, Culture={Culture}", reference, id, culture);

        try
        {
            await subscriptionService.MarkEmailSentAsync(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warning(ex, "Newsletter subscription {Reference} was emailed but EmailSent could not be set. Id={Id}", reference, id);
        }
    }

    private async Task<bool> TrySendAsync(string to, string? replyTo, string subject, string body, string reference, int id, string who)
    {
        try
        {
            await emailHelper.SendAsync(to, replyTo, subject, body, null, reference, CancellationToken.None);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Newsletter subscription {Reference} was saved (Id={Id}) but the email to the {Who} failed.", reference, id, who);
            return false;
        }
    }

    private static HttpStatusException Unavailable() => new(HttpStatusCode.ServiceUnavailable, GeneralConstants.ServiceUnavailableMessage);

    private static HttpStatusException Failed() => new(HttpStatusCode.InternalServerError, GeneralConstants.UnexpectedErrorMessage);

    [GeneratedRegex(@"\{\{Email\}\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailPlaceholder();

    #endregion
}
