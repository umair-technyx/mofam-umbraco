using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;

namespace Mofam.Application.IServices;
public interface IContactService
{
    Task<ContactReceiptDto> SubmitAsync(ContactRequest request, string culture, string? clientIp, string reference, CancellationToken cancellationToken);
}
