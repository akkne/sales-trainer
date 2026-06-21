using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Returns "today" in the product-configured streak timezone.
/// The timezone is read from Gamification:StreakTimezone (IANA or Windows id, default "UTC").
/// A single product-wide timezone is intentional — streaks are a product concept, not per-user.
/// </summary>
internal sealed class StreakClock : IStreakClock
{
    private readonly TimeZoneInfo _timeZone;

    public StreakClock(IConfiguration configuration)
    {
        var tzId = configuration["Gamification:StreakTimezone"];
        _timeZone = string.IsNullOrWhiteSpace(tzId)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }

    public DateOnly Today()
    {
        var nowInZone = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
        return DateOnly.FromDateTime(nowInZone);
    }
}
