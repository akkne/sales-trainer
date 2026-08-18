namespace Sellevate.Gamification.Infrastructure.Configuration;

/// <summary>
/// The timezone the streak day boundary is measured in.
///
/// <para>
/// A single product-wide timezone is intentional, not a simplification waiting to be fixed: a streak
/// is a product promise ("you practised today"), and making the boundary per-user would let two
/// people in the same team see different day counts for the same activity. Changing it moves every
/// user's boundary at once, which is the only coherent way to change it at all.
/// </para>
/// </summary>
public sealed class StreakConfiguration
{
    public const string SectionName = "Gamification";

    /// <summary>
    /// IANA or Windows timezone identifier. Blank means UTC; an identifier the host cannot resolve
    /// fails loudly at startup rather than silently falling back, because a silently wrong boundary
    /// would reset streaks a day early for everybody.
    /// </summary>
    public string StreakTimezone { get; init; } = DefaultStreakTimezone;

    public const string DefaultStreakTimezone = "UTC";
}
