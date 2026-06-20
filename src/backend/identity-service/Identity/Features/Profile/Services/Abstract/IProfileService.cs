using Sellevate.Identity.Features.Profile.Models;

namespace Sellevate.Identity.Features.Profile.Services.Abstract;

public interface IProfileService
{
    Task<UserProfileStatsDto> GetProfileStatsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdatePersonaForUserAsync(
        Guid userId,
        string persona,
        CancellationToken cancellationToken = default);
}
