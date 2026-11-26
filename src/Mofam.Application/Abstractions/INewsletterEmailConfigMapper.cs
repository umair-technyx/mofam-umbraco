namespace Mofam.Application.Abstractions;

public sealed record NewsletterEmailConfig(string AdminEmail, string Subject, string AdminBody, string UserBody);

public interface INewsletterEmailConfigMapper
{
    NewsletterEmailConfig? TryGet(string culture, out string? failure);

    bool IsEmailEnabled(string culture);
}
