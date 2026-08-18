namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// Provides the "today" date for streak logic, computed in the product-configured timezone
/// (<c>Gamification:StreakTimezone</c>, default UTC).
///
/// <para>
/// The seam exists so <c>StreakService</c> and <c>StreakResetJob</c> can never disagree about where a
/// day ends — a disagreement would zero a streak the same night it was earned — and so the boundary is
/// unit-testable without touching system time.
/// </para>
/// </summary>
public interface IStreakClock
{
    DateOnly Today();
}
