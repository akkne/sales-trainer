namespace SalesTrainer.Api.Features.Admin;

public record AdminLeagueListItemDto(
    Guid Id,
    string Tier,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    int MemberCount
);

public record AdminLeagueMemberDto(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    string Email,
    int WeeklyXpAmount,
    int Rank,
    string? PromotionOutcome
);

public record AdminLeagueDetailDto(
    Guid Id,
    string Tier,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    List<AdminLeagueMemberDto> Members
);

public record MoveMembershipTierRequestDto(string Tier);

public record AdjustMembershipXpRequestDto(int Delta);

public record LeagueSettingsDto(
    int MaximumLeagueParticipantCount,
    int PromotionZoneSize,
    int DemotionZoneSize,
    DateTimeOffset? CurrentPeriodEndsAt,
    int PeriodLengthDays
);

public record UpdateLeagueSettingsRequestDto(
    int MaximumLeagueParticipantCount,
    int PromotionZoneSize,
    int DemotionZoneSize,
    // Optional: only applied when provided, so callers may update zones alone.
    DateTimeOffset? CurrentPeriodEndsAt,
    int? PeriodLengthDays
);

public record AdminLeagueTierDto(
    Guid Id,
    string Key,
    string Name,
    string Color,
    int Order
);

public record CreateLeagueTierRequestDto(
    string Key,
    string Name,
    string Color,
    int Order
);

public record UpdateLeagueTierRequestDto(
    string Name,
    string Color,
    int Order
);
