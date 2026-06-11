namespace SalesTrainer.Api.Features.Discuss.Models;

public sealed record TagRefDto(string Slug, string Name);

public sealed record DiscussThreadSummaryDto(
    Guid Id,
    string Title,
    string BodyPreview,
    Guid AuthorId,
    string AuthorName,
    int UpvoteCount,
    int ReplyCount,
    int ViewCount,
    bool IsPinned,
    bool IsHot,
    bool IsSolved,
    IReadOnlyList<TagRefDto> Tags,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    bool ViewerHasUpvoted);

public sealed record DiscussReplyDto(
    Guid Id,
    Guid ThreadId,
    Guid AuthorId,
    string AuthorName,
    string Body,
    int UpvoteCount,
    bool IsAccepted,
    DateTime CreatedAt,
    bool ViewerHasUpvoted);

public sealed record DiscussThreadDetailDto(
    Guid Id,
    string Title,
    string Body,
    Guid AuthorId,
    string AuthorName,
    int UpvoteCount,
    int ReplyCount,
    int ViewCount,
    bool IsPinned,
    bool IsHot,
    bool IsSolved,
    Guid? AcceptedReplyId,
    IReadOnlyList<TagRefDto> Tags,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    bool ViewerHasUpvoted,
    IReadOnlyList<DiscussReplyDto> Replies);

public sealed record DiscussTagDto(Guid Id, string Slug, string Name, bool IsCurated);

public sealed record PopularTagDto(string Slug, string Name, int ThreadCount);

public sealed record TopAuthorDto(Guid AuthorId, string AuthorName, int UpvotesReceived);

public sealed record DiscussStatsDto(
    int TotalThreads,
    int TotalReplies,
    IReadOnlyList<TopAuthorDto> TopAuthorsOfWeek);

public sealed record VoteResultDto(int UpvoteCount, bool HasUpvoted);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
