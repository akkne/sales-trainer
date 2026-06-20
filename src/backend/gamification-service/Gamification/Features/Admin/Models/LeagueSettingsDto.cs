namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record LeagueSettingsDto(
    int MaximumLeagueParticipantCount,
    int PromotionZoneSize,
    int DemotionZoneSize,
    DateTimeOffset? CurrentPeriodEndsAt,
    int PeriodLengthDays);
