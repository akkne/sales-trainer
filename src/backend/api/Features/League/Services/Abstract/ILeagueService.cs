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

    /// <summary>
    /// Closes the current period and creates the next one only if the configured
    /// period end moment has passed. Invoked by the recurring rollover job.
    /// </summary>
    Task RolloverIfDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the singleton settings row, creating it and initializing the current
    /// period (start/end) on first access. The returned entity is tracked by the
    /// request's <c>AppDbContext</c>, so callers can mutate and save it.
    /// </summary>
    Task<LeagueSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
}
