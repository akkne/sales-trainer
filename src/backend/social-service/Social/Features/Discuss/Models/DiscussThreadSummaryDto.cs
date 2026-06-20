namespace Sellevate.Social.Features.Discuss.Models;

public sealed record DiscussThreadSummaryDto(
    Guid Id,
    string Title,
    string BodyPreview,
    Guid AuthorId,
    string AuthorName,
    string AuthorAvatarUrl,
    int UpvoteCount,
    int ReplyCount,
    int ViewCount,
    bool IsPinned,
    bool IsHot,
    bool IsSolved,
    IReadOnlyList<TagRefDto> Tags,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    bool ViewerHasUpvoted,
    int PhotoCount,
    string? FirstPhotoUrl);
