namespace SalesTrainer.Api.Features.League.Models;

public sealed class LeagueSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MaximumLeagueParticipantCount { get; set; } = 30;
    public int PromotionZoneSize { get; set; } = 10;
    public int DemotionZoneSize { get; set; } = 5;

    /// <summary>Start of the currently running league period. Null until first initialized.</summary>
    public DateOnly? CurrentPeriodStartDate { get; set; }

    /// <summary>
    /// Exact moment the current period ends. Drives the league-tab countdown and the
    /// rollover trigger. Admin-editable so periods can follow an arbitrary schedule.
    /// </summary>
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }

    /// <summary>Length in days applied to each new period when one is created on rollover.</summary>
    public int PeriodLengthDays { get; set; } = 7;
}
