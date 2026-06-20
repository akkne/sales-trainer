namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record AdminLeagueMemberDto(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    string Email,
    int WeeklyXpAmount,
    int Rank,
    string? PromotionOutcome);
