namespace Sellevate.Gamification.Features.League.Models;

public sealed class LeagueSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MaximumLeagueParticipantCount { get; set; } = 30;
    public int PromotionZoneSize { get; set; } = 10;
    public int DemotionZoneSize { get; set; } = 5;
    public DateOnly? CurrentPeriodStartDate { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public int PeriodLengthDays { get; set; } = 7;
}
