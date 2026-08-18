namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// Records that a user was active today and maintains their consecutive-day counts.
///
/// <para>
/// Registration is idempotent per day: call it on every completed activity. The day boundary comes
/// from <see cref="IStreakClock"/>, so it is the product's timezone rather than the host's.
/// </para>
/// </summary>
public interface IStreakService
{
    Task RegisterActivityAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetCurrentStreakDayCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
