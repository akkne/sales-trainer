namespace Sellevate.Gamification.Common.Extensions;

/// <summary>
/// Calendar arithmetic shared by the league period and the progress read model.
/// </summary>
public static class DateOnlyExtensions
{
    /// <summary>
    /// The Monday of the ISO week containing <paramref name="date"/>. Monday-based on purpose: the
    /// league period and the weekly experience-point goal are the same week, so both must agree on
    /// where a week begins, and a Sunday-based boundary would move a Sunday's points into the
    /// following league week.
    /// </summary>
    public static DateOnly StartOfWeek(this DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + DaysFromSundayToMonday) % DaysPerWeek;
        return date.AddDays(-daysSinceMonday);
    }

    private const int DaysPerWeek = 7;

    /// <summary>
    /// <see cref="DayOfWeek"/> numbers Sunday as zero; adding six before the modulo rotates the
    /// week so Monday becomes zero.
    /// </summary>
    private const int DaysFromSundayToMonday = 6;
}
