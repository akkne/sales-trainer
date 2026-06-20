namespace Sellevate.Social.Features.Discuss.Models;

public sealed record DiscussReplyDto(
    Guid Id,
    Guid ThreadId,
    Guid AuthorId,
    string AuthorName,
    string AuthorAvatarUrl,
    string Body,
    int UpvoteCount,
    bool IsAccepted,
    DateTime CreatedAt,
    bool ViewerHasUpvoted,
    IReadOnlyList<DiscussPhotoDto> Photos);
