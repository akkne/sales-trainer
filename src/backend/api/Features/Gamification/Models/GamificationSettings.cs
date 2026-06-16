namespace SalesTrainer.Api.Features.Gamification.Models;

/// <summary>
/// Singleton row holding tunable, admin-editable XP/gamification knobs that were
/// previously hardcoded. Exactly one row exists; it is loaded-or-created on demand,
/// mirroring the LeagueSettings pattern.
/// </summary>
public sealed class GamificationSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Daily XP goal shown on the skill tree progress ring.</summary>
    public int DailyXpGoal { get; set; } = 100;

    /// <summary>Weekly XP goal shown on the skill tree.</summary>
    public int WeeklyXpGoal { get; set; } = 500;

    /// <summary>
    /// Multiplier applied to the AI's raw 0-100 dialog score to produce the XP a user
    /// actually earns for a completed dialog (e.g. 1.0 = score is XP, 1.5 = 50% bonus).
    /// </summary>
    public double DialogXpMultiplier { get; set; } = 1.0;

    // Per-criterion maximums injected into the dialog feedback prompt. They tell the AI
    // how many points each aspect of the call is worth; the raw sum is the dialog score.
    public int DialogWeightConfidence { get; set; } = 25;
    public int DialogWeightStructure { get; set; } = 25;
    public int DialogWeightObjection { get; set; } = 25;
    public int DialogWeightGoal { get; set; } = 25;
}
