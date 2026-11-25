using System.Net.Http.Json;
using System.Text;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.RecaptchaEnterprise.V1;
using Microsoft.Extensions.Options;
using Mofam.Application.IServices;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Options;
using static Mofam.Domain.Constants.GeneralConstants;

namespace Mofam.Application.Services;
public sealed class RecaptchaService(IHttpClientFactory httpClientFactory, IOptions<ReCaptchaOptions> options, Serilog.ILogger logger) : IRecaptchaService
{
    private readonly Lazy<RecaptchaEnterpriseServiceClient> _client = new(() => Build(options.Value), LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<RecaptchaResult> VerifyAsync(string? token, string? clientIp, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.Enabled) return new RecaptchaResult(RecaptchaStatus.Skipped);

        if (!settings.IsConfigured)
        {
            return new RecaptchaResult(
                RecaptchaStatus.Unavailable,
                settings.VerifyViaSecretKey
                    ? "reCAPTCHA is turned on, but SiteKey or SecretKey is not set."
                    : "reCAPTCHA is turned on, but ProjectId, SiteKey or CredentialsJsonBase64 is not set.");
        }

        if (string.IsNullOrWhiteSpace(token)) return new RecaptchaResult(RecaptchaStatus.Failed, "The token is missing.");

        try
        {
            return settings.VerifyViaSecretKey
                ? await VerifyWithSecretKeyAsync(settings, token, clientIp, cancellationToken)
                : await VerifyWithAssessmentAsync(settings, token, clientIp, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "The reCAPTCHA check could not be done.");
            return new RecaptchaResult(RecaptchaStatus.Unavailable, "The check could not be done.");
        }
    }

    private async Task<RecaptchaResult> VerifyWithSecretKeyAsync(ReCaptchaOptions settings, string token, string? clientIp, CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", settings.SecretKey),
            new("response", token),
        };

        if (!string.IsNullOrWhiteSpace(clientIp)) form.Add(new("remoteip", clientIp));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        using var content = new FormUrlEncodedContent(form);
        using var response = await httpClientFactory.CreateClient().PostAsync("https://www.google.com/recaptcha/api/siteverify", content, timeout.Token);

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

        var score = result.Score ?? 0;

        return score >= 0.5
            ? new RecaptchaResult(RecaptchaStatus.Passed, Score: (float)score)
            : new RecaptchaResult(RecaptchaStatus.Failed, "The score is too low.", (float)score);
    }

    private async Task<RecaptchaResult> VerifyWithAssessmentAsync(ReCaptchaOptions settings, string token, string? clientIp, CancellationToken cancellationToken)
    {
        var request = new CreateAssessmentRequest
        {
            ParentAsProjectName = new ProjectName(settings.ProjectId),
            Assessment = new Assessment
            {
                Event = new Google.Cloud.RecaptchaEnterprise.V1.Event
                {
                    SiteKey = settings.SiteKey,
                    Token = token,
                    ExpectedAction = "submit",
                    UserIpAddress = clientIp ?? string.Empty,
                },
            },
        };

        var callSettings = CallSettings
            .FromCancellationToken(cancellationToken)
            .MergedWith(CallSettings.FromExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(5))));

        var response = await _client.Value.CreateAssessmentAsync(request, callSettings);

        if (!response.TokenProperties.Valid)
        {
            return new RecaptchaResult(RecaptchaStatus.Failed, response.TokenProperties.InvalidReason.ToString());
        }

        if (!string.Equals(response.TokenProperties.Action, "submit", StringComparison.Ordinal))
        {
            return new RecaptchaResult(RecaptchaStatus.Failed, "The action does not match.");
        }

        var score = response.RiskAnalysis.Score;

        return score >= 0.5
            ? new RecaptchaResult(RecaptchaStatus.Passed, Score: score)
            : new RecaptchaResult(RecaptchaStatus.Failed, "The score is too low.", score);
    }

    private static RecaptchaEnterpriseServiceClient Build(ReCaptchaOptions settings)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(settings.CredentialsJsonBase64));

        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential();

        return new RecaptchaEnterpriseServiceClientBuilder { GoogleCredential = credential }.Build();
    }
}
