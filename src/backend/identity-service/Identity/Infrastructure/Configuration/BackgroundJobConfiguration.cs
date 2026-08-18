namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// Sweep intervals for identity-service's two timer-driven cleanup jobs. Both delete rows that are
/// already unusable, so the interval only trades table size against write load and can be tuned per
/// environment without a rebuild. A non-positive value is rejected by <see cref="PeriodicTimer"/>,
/// so treat these as "hours, at least one".
/// </summary>
public sealed class BackgroundJobConfiguration
{
    public const string SectionName = "BackgroundJobs";

    public int ExpiredRefreshTokenCleanupIntervalHours { get; init; } = 24;

    public int ExpiredEmailVerificationCleanupIntervalHours { get; init; } = 24;
}
