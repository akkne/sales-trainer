using Microsoft.Extensions.Options;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Configuration;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Returns "today" in the product-configured streak timezone.
///
/// <para>
/// The timezone is resolved once, in the constructor, which is why this is registered as a singleton:
/// a streak boundary that moved mid-process would let two requests in the same second disagree about
/// what day it is. Changing <c>Gamification:StreakTimezone</c> therefore requires a restart.
/// </para>
/// </summary>
internal sealed class StreakClock : IStreakClock
{
    private readonly TimeZoneInfo _timeZone;

    public StreakClock(IOptions<StreakConfiguration> streakConfiguration)
    {
        ArgumentNullException.ThrowIfNull(streakConfiguration);

        var timeZoneId = streakConfiguration.Value.StreakTimezone;
        _timeZone = string.IsNullOrWhiteSpace(timeZoneId)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateOnly Today()
    {
        var nowInZone = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
        return DateOnly.FromDateTime(nowInZone);
    }
}
