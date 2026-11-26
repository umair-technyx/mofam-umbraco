using Mofam.Domain.Models.Dtos;
using Mofam.Domain.Models.Requests;

namespace Mofam.Application.IServices;
public interface INewsletterService
{
    Task<NewsletterReceiptDto> SubscribeAsync(NewsletterRequest request, string culture, string? clientIp, string reference, CancellationToken cancellationToken);
}
