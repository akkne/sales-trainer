namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record AdminLeagueDetailDto(
    Guid Id,
    string Tier,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    IReadOnlyList<AdminLeagueMemberDto> Members);
