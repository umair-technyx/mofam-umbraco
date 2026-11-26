using Mofam.Application.IServices;
using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Mofam.Application.Services;

public sealed class NewsletterSubscriptionService(IUmbracoDatabaseFactory databaseFactory) : INewsletterSubscriptionService
{
    public async Task<NewsletterSaveResult> SaveAsync(string email, string culture)
    {
        using var database = databaseFactory.CreateDatabase();

        var rows = await database.FetchAsync<NewsletterSaveResult>("EXEC dbo.usp_NewsletterSubscriber_Insert @0, @1", [email, culture]);

        return rows.Single();
    }

    public async Task MarkEmailSentAsync(int id)
    {
        using var database = databaseFactory.CreateDatabase();

        await database.ExecuteAsync("EXEC dbo.usp_NewsletterSubscriber_MarkEmailSent @0", [id]);
    }

    public async Task<IReadOnlyList<NewsletterSubscriberDto>> GetAllAsync(string culture)
    {
        using var database = databaseFactory.CreateDatabase();

        return await database.FetchAsync<NewsletterSubscriberDto>("EXEC dbo.usp_NewsletterSubscriber_GetAll @0", [culture]);
    }
}
