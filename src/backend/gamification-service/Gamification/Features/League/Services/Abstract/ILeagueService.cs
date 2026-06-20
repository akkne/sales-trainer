using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Features.League.Services.Abstract;

public interface ILeagueService
{
    Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task CloseCurrentLeagueAndCreateNextAsync(CancellationToken cancellationToken = default);

    Task RolloverIfDueAsync(CancellationToken cancellationToken = default);

    Task SyncLeagueWeeklyExperiencePointsAsync(Guid leagueId, CancellationToken cancellationToken = default);

    Task<LeagueSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
}
