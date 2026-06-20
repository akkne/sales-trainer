namespace Sellevate.Social.Features.Discuss.Models;

public sealed record DiscussThreadDetailDto(
    Guid Id,
    string Title,
    string Body,
    Guid AuthorId,
    string AuthorName,
    string AuthorAvatarUrl,
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
    IReadOnlyList<DiscussReplyDto> Replies,
    IReadOnlyList<DiscussPhotoDto> Photos);
