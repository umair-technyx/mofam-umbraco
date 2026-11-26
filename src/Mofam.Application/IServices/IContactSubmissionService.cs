using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;

namespace Mofam.Application.IServices;

public interface IContactSubmissionService
{
    Task<int> SaveAsync(ContactRequest data, string culture);

    Task MarkEmailSentAsync(int id);

    Task<IReadOnlyList<ContactSubmissionDto>> GetAllAsync(string culture);
}
