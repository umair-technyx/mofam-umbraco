using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.IServices;

public interface INewsletterSubscriptionService
{
    Task<NewsletterSaveResult> SaveAsync(string email, string culture);

    Task MarkEmailSentAsync(int id);

    Task<IReadOnlyList<NewsletterSubscriberDto>> GetAllAsync(string culture);
}
