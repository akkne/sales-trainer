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
    int DemotionZoneSize
);

public record UpdateLeagueSettingsRequestDto(
    int MaximumLeagueParticipantCount,
    int PromotionZoneSize,
    int DemotionZoneSize
);
