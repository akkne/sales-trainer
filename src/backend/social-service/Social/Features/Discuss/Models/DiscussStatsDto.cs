namespace Sellevate.Social.Features.Discuss.Models;

public sealed record DiscussStatsDto(
    int TotalThreads,
    int TotalReplies,
    IReadOnlyList<TopAuthorDto> TopAuthorsOfWeek);
