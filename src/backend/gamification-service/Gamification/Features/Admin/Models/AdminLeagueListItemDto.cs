namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record AdminLeagueListItemDto(
    Guid Id,
    string Tier,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    int MemberCount);
