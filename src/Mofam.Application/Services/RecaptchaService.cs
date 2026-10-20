using System.Text;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.RecaptchaEnterprise.V1;
using Microsoft.Extensions.Options;
using Mofam.Application.IServices;
using Mofam.Domain.Options;
using static Mofam.Domain.Constants.GeneralConstants;

namespace Mofam.Application.Services;
public sealed class RecaptchaService(IOptions<ReCaptchaOptions> options, Serilog.ILogger logger) : IRecaptchaService
{
    private readonly Lazy<RecaptchaEnterpriseServiceClient> _client = new(() => Build(options.Value), LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<RecaptchaResult> VerifyAsync(string? token, string? clientIp, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.Enabled) return new RecaptchaResult(RecaptchaStatus.Skipped);

        if (!settings.IsConfigured)
        {
            return new RecaptchaResult(RecaptchaStatus.Unavailable, "reCAPTCHA is turned on, but ProjectId, SiteKey or CredentialsJsonBase64 is not set.");
        }

        if (string.IsNullOrWhiteSpace(token)) return new RecaptchaResult(RecaptchaStatus.Failed, "The token is missing.");

        try
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

            return score >= 0.5f
                ? new RecaptchaResult(RecaptchaStatus.Passed, Score: score)
                : new RecaptchaResult(RecaptchaStatus.Failed, "The score is too low.", score);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            // The visitor closed the page: Google reports it as a gRPC "Cancelled" error. This is normal, so it is passed on as a cancel and is not logged.
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex)
        {
            // Google was not reachable, or the credentials are wrong. The details stay in the log, the visitor gets a generic message.
            logger.Error(ex, "The reCAPTCHA check could not be done.");
            return new RecaptchaResult(RecaptchaStatus.Unavailable, "The check could not be done.");
        }
    }

    private static RecaptchaEnterpriseServiceClient Build(ReCaptchaOptions settings)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(settings.CredentialsJsonBase64));

        // The service account file is read into a credential that Google can use. A wrong file stops here.
        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential();

        return new RecaptchaEnterpriseServiceClientBuilder { GoogleCredential = credential }.Build();
    }
}
