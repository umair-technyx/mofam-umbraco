using Mofam.Application.IServices;
using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Mofam.Application.Services;

public sealed class ContactSubmissionService(IUmbracoDatabaseFactory databaseFactory) : IContactSubmissionService
{
    public async Task<int> SaveAsync(ContactRequest data, string culture)
    {
        using var database = databaseFactory.CreateDatabase();

        return await database.ExecuteScalarAsync<int>(
            "EXEC dbo.usp_ContactSubmission_Insert @0, @1, @2, @3, @4, @5, @6",
            [data.Subject ?? string.Empty, data.FirstName ?? string.Empty, data.LastName ?? string.Empty, data.Email ?? string.Empty, data.ContactNumber ?? string.Empty, data.Message ?? string.Empty, culture]);
    }

    public async Task MarkEmailSentAsync(int id)
    {
        using var database = databaseFactory.CreateDatabase();

        await database.ExecuteAsync("EXEC dbo.usp_ContactSubmission_MarkEmailSent @0", [id]);
    }

    public async Task<IReadOnlyList<ContactSubmissionDto>> GetAllAsync(string culture)
    {
        using var database = databaseFactory.CreateDatabase();

        return await database.FetchAsync<ContactSubmissionDto>("EXEC dbo.usp_ContactSubmission_GetAll @0", [culture]);
    }
}
