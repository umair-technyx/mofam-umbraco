using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.IServices;

/// <summary>
/// Builds the front end's first-load payload: header, footer, dictionary and culture.
/// </summary>
public interface IStartupService
{
    Task<Startup?> GetStartupAsync(string? culture);
}
