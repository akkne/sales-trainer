using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// Test double for IStreakClock that returns a fixed date.
/// Defaults to DateOnly.FromDateTime(DateTime.UtcNow) so existing tests
/// that don't care about timezone still work unchanged.
/// </summary>
internal sealed class FixedStreakClock(DateOnly? fixedDate = null) : IStreakClock
{
    private readonly DateOnly _date = fixedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly Today() => _date;
}
