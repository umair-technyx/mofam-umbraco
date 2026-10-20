namespace Mofam.Application.Abstractions;
public sealed record ContactEmailConfig(string AdminEmail, string AdminSubject, string AdminBody);

public interface IContactEmailConfigMapper
{
    ContactEmailConfig? TryGet(string culture, out string? failure);
}
