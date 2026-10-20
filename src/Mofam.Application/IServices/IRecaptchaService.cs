using static Mofam.Domain.Constants.GeneralConstants;

namespace Mofam.Application.IServices;

public sealed record RecaptchaResult(RecaptchaStatus Status, string? Reason = null, float? Score = null);
public interface IRecaptchaService
{
    Task<RecaptchaResult> VerifyAsync(string? token, string? clientIp, CancellationToken cancellationToken);
}
