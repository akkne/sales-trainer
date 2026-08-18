using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Features.League.Services.Abstract;

/// <summary>
/// The weekly league: which cohort a user is in, what everybody in it has earned this period, and how
/// one period closes into the next.
///
/// <para>
/// Reads create on demand — asking for a user's current league places them in one if the period has
/// no league for their tier yet — and every creation path tolerates a concurrent first-hit. Both
/// <see cref="CloseCurrentLeagueAndCreateNextAsync"/> and <see cref="RolloverIfDueAsync"/> are safe to
/// call repeatedly: they return without effect once the period has already advanced, which is what
/// lets a frequent cron and an admin button coexist.
/// </para>
/// </summary>
public interface ILeagueService
{
    Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task CloseCurrentLeagueAndCreateNextAsync(CancellationToken cancellationToken = default);

    Task RolloverIfDueAsync(CancellationToken cancellationToken = default);

    Task SyncLeagueWeeklyExperiencePointsAsync(Guid leagueId, CancellationToken cancellationToken = default);

    Task<LeagueSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
}
