using SalesTrainer.Api.Features.League.Models;

namespace SalesTrainer.Api.Features.League.Services.Abstract;

public interface ILeagueService
{
    Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task CloseCurrentLeagueAndCreateNextAsync(
        CancellationToken cancellationToken = default);

    Task SyncLeagueWeeklyXpAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default);
}
