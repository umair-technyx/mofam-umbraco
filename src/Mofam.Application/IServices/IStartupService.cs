using Mofam.Domain.Models.Dtos;

namespace Mofam.Application.IServices;

// Builds the data the front end needs on first load: header, footer, dictionary and culture.
public interface IStartupService
{
    #region Startup

    Task<Startup?> GetStartupAsync(string? culture);

    #endregion
}
