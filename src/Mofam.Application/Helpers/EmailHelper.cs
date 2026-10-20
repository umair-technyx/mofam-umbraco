using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Mofam.Domain.Constants;
using Mofam.Domain.Exceptions;
using Mofam.Domain.Models.Requests;
using Mofam.Domain.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Mofam.Application.Helpers;
public class EmailHelper(HttpClient httpClient, IOptions<EmailSettings> options, Serilog.ILogger logger)
{
    public virtual async Task SendAsync(
        string to, string? replyTo, string subject, string htmlBody, IReadOnlyList<EmailAttachment>? attachments, string reference, CancellationToken cancellationToken)
    {
        var email = options.Value;
        var from = email.SenderAddress.Trim();
        var fromName = string.IsNullOrWhiteSpace(email.SenderName) ? null : email.SenderName.Trim();

        // A line break in a header value (an address, the name, the subject) could add new headers to the email (header injection).
        if (!CommonHelper.IsValidEmail(from)) throw Failed(reference, "EmailSettings:SenderAddress is missing or is not a valid email address.");
        if (!CommonHelper.IsValidEmail(to)) throw Failed(reference, "The address to send to is not a valid email address.");
        if (replyTo is not null && !CommonHelper.IsValidEmail(replyTo)) throw Failed(reference, "The reply-to address is not a valid email address.");
        if (CommonHelper.HasLineBreak(fromName) || CommonHelper.HasLineBreak(subject)) throw Failed(reference, "The name or the subject of the email has a line break or a control character.");
        if (CommonHelper.IsBlank(subject) || CommonHelper.IsBlank(htmlBody)) throw Failed(reference, "The email has no subject or no body.");

        if (email.UseSendGrid)
        {
            await SendWithSendGridAsync(from, fromName, to, replyTo, subject, htmlBody, attachments, reference, cancellationToken);
        }
        else
        {
            await SendWithSmtpAsync(from, fromName, to, replyTo, subject, htmlBody, attachments, reference, cancellationToken);
        }
    }

    #region SMTP

    private async Task SendWithSmtpAsync(
        string from, string? fromName, string to, string? replyTo, string subject, string htmlBody, IReadOnlyList<EmailAttachment>? attachments, string reference, CancellationToken cancellationToken)
    {
        var smtp = options.Value.Smtp;
        if (CommonHelper.IsBlank(smtp.Host)) throw Failed(reference, "SMTP is not set up (EmailSettings:Smtp:Host).");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName ?? string.Empty, from));
        message.To.Add(new MailboxAddress(string.Empty, to));
        if (replyTo is not null) message.ReplyTo.Add(new MailboxAddress(string.Empty, replyTo));
        message.Subject = subject;

        var body = new BodyBuilder { HtmlBody = htmlBody };
        // A content type that cannot be read is sent as a general binary file.
        foreach (var file in attachments ?? []) body.Attachments.Add(file.FileName, file.Content, ContentType.TryParse(file.ContentType, out var type) ? type : new ContentType("application", "octet-stream"));
        message.Body = body.ToMessageBody();

        var socketOptions = smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : smtp.Port == 587 ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        // MailKit can be cancelled, so the timeout really stops the connection and the send.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            using var client = new SmtpClient();

            await client.ConnectAsync(smtp.Host.Trim(), smtp.Port, socketOptions, timeout.Token);

            if (!CommonHelper.IsBlank(smtp.Username)) await client.AuthenticateAsync(smtp.Username, smtp.Password, timeout.Token);

            await client.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable(reference, "The SMTP server did not answer in time.");
        }
        catch (Exception ex) when (ex is AuthenticationException or SslHandshakeException)
        {
            // A wrong user name or password, or a certificate that is not trusted: a setting problem, trying again will not help.
            throw Failed(reference, $"The SMTP login or the SSL connection is wrong ({ex.GetType().Name}).");
        }
        catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
        {
            // The server refused the email (for example the sender is not allowed). A new try gets the same answer.
            throw Failed(reference, $"The SMTP server refused the email (status {(int)ex.StatusCode}).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Only the type is kept. The message of the exception can have server details.
            throw Unavailable(reference, $"The SMTP send failed ({ex.GetType().Name}).");
        }
    }

    #endregion

    #region SendGrid
    private async Task SendWithSendGridAsync(
        string from, string? fromName, string to, string? replyTo, string subject, string htmlBody, IReadOnlyList<EmailAttachment>? attachments, string reference, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var apiKey = settings.SendGrid.ApiKey.Trim();

        if (apiKey.Length == 0) throw Failed(reference, "The SendGrid API key is not set (EmailSettings:SendGrid:ApiKey).");

        var host = settings.SendGrid.BaseAddress.Trim().TrimEnd('/');
        var client = new SendGridClient(httpClient, new SendGridClientOptions { ApiKey = apiKey, Host = host });

        var message = MailHelper.CreateSingleEmail(new EmailAddress(from, fromName), new EmailAddress(to), subject, null, htmlBody);
        if (replyTo is not null) message.SetReplyTo(new EmailAddress(replyTo));
        foreach (var file in attachments ?? []) message.AddAttachment(file.FileName, Convert.ToBase64String(file.Content), file.ContentType, "attachment");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        Response response;
        try
        {
            response = await client.SendEmailAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable(reference, "SendGrid did not answer in time.");
        }
        catch (HttpRequestException)
        {
            throw Unavailable(reference, "SendGrid could not be reached.");
        }

        // SendGrid answers 202 when it has accepted the email. The body is not read or logged.
        if (response.IsSuccessStatusCode) return;

        var status = (int)response.StatusCode;

        throw status switch
        {
            401 or 403 => Failed(reference, $"SendGrid refused the API key or the sender address (HTTP {status})."),
            408 or 429 or >= 500 => Unavailable(reference, $"SendGrid is busy or down (HTTP {status})."),
            _ => Failed(reference, $"SendGrid refused the email (HTTP {status})."),
        };
    }

    #endregion

    #region Failures

    // The provider is slow or down: the visitor can try again later (503).
    private HttpStatusException Unavailable(string reference, string reason)
    {
        logger.Warning("Email {Reference}: the email provider is not available. {Reason}", reference, reason);
        return new HttpStatusException(HttpStatusCode.ServiceUnavailable, GeneralConstants.ServiceUnavailableMessage);
    }

    // A setting or the email is wrong: trying again will not help (500).
    private HttpStatusException Failed(string reference, string reason)
    {
        logger.Error("Email {Reference}: the email could not be sent. {Reason}", reference, reason);
        return new HttpStatusException(HttpStatusCode.InternalServerError, GeneralConstants.UnexpectedErrorMessage);
    }

    #endregion
}
