namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// Provides the "today" date for streak logic, computed in the product-configured
/// timezone (Gamification:StreakTimezone in appsettings.json, default UTC).
/// Using a seam here keeps both StreakService and StreakResetJob consistent and
/// makes the timezone boundary unit-testable without touching system time.
/// </summary>
public interface IStreakClock
{
    DateOnly Today();
}
