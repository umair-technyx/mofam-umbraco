using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Mofam.Application.IServices;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using static Mofam.Domain.Constants.GeneralConstants;

namespace Mofam.Application.Services;
public sealed class RecaptchaService(HttpClient httpClient, IOptions<ReCaptchaOptions> options, Serilog.ILogger logger) : IRecaptchaService
{
    public async Task<RecaptchaResult> VerifyAsync(string? token, string? clientIp, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.Enabled) return new RecaptchaResult(RecaptchaStatus.Skipped);

        if (!settings.IsConfigured)
        {
            return new RecaptchaResult(RecaptchaStatus.Unavailable, "reCAPTCHA is turned on, but SiteKey or SecretKey is not set.");
        }

        if (string.IsNullOrWhiteSpace(token)) return new RecaptchaResult(RecaptchaStatus.Failed, "The token is missing.");

        try
        {
            // The siteverify endpoint of reCAPTCHA (it accepts the Enterprise legacy secret key).
            const string verifyUrl = "https://www.google.com/recaptcha/api/siteverify";

            var form = new List<KeyValuePair<string, string>>
            {
                new("secret", settings.SecretKey),
                new("response", token),
            };

            if (!string.IsNullOrWhiteSpace(clientIp)) form.Add(new("remoteip", clientIp));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClient.PostAsync(verifyUrl, content, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                logger.Error("The reCAPTCHA check could not be done. Google answered {StatusCode}.", (int)response.StatusCode);
                return new RecaptchaResult(RecaptchaStatus.Unavailable, "The check could not be done.");
            }

            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(timeout.Token);

            if (result is null)
            {
                logger.Error("The reCAPTCHA check could not be done. The answer from Google was empty.");
                return new RecaptchaResult(RecaptchaStatus.Unavailable, "The check could not be done.");
            }

            if (!result.Success)
            {
                var errors = result.ErrorCodes ?? [];

                // These error codes mean the server is set up wrong, not that the visitor failed the check.
                if (errors.Any(e => e is "missing-input-secret" or "invalid-input-secret"))
                {
                    logger.Error("The reCAPTCHA secret key was rejected. ErrorCodes={ErrorCodes}", string.Join(",", errors));
                    return new RecaptchaResult(RecaptchaStatus.Unavailable, "The check could not be done.");
                }

                return new RecaptchaResult(RecaptchaStatus.Failed, errors.Count > 0 ? string.Join(",", errors) : "The token is not valid.");
            }

            if (!string.Equals(result.Action, "submit", StringComparison.Ordinal))
            {
                return new RecaptchaResult(RecaptchaStatus.Failed, "The action does not match.");
            }

            if (settings.AllowedHostnames.Length > 0
                && !settings.AllowedHostnames.Contains(result.Hostname ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                return new RecaptchaResult(RecaptchaStatus.Failed, "The hostname is not allowed.");
            }

            var score = result.Score ?? 0;

            return score >= settings.MinimumScore
                ? new RecaptchaResult(RecaptchaStatus.Passed, Score: (float)score)
                : new RecaptchaResult(RecaptchaStatus.Failed, "The score is too low.", (float)score);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "The reCAPTCHA check could not be done.");
            return new RecaptchaResult(RecaptchaStatus.Unavailable, "The check could not be done.");
        }
    }
}
