using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Services.Abstract;

public interface IDiscussService
{
    Task<PagedResultDto<DiscussThreadSummaryDto>> ListThreadsAsync(
        DiscussThreadQuery query, Guid viewerId, CancellationToken cancellationToken = default);

    Task<DiscussThreadDetailDto?> GetThreadAsync(
        Guid threadId, Guid viewerId, bool incrementView, CancellationToken cancellationToken = default);

    Task<DiscussThreadDetailDto> CreateThreadAsync(
        Guid authorId, string title, string body, IReadOnlyList<string> tagLabels, CancellationToken cancellationToken = default);

    Task<DiscussReplyDto?> AddReplyAsync(
        Guid threadId, Guid authorId, string body, CancellationToken cancellationToken = default);

    Task<VoteResultDto?> SetThreadVoteAsync(Guid threadId, Guid userId, bool upvote, CancellationToken cancellationToken = default);
    Task<VoteResultDto?> SetReplyVoteAsync(Guid replyId, Guid userId, bool upvote, CancellationToken cancellationToken = default);

    Task<(DiscussOperationStatus Status, DiscussThreadDetailDto? Thread)> SetAcceptedReplyAsync(
        Guid threadId, Guid actingUserId, bool isAdmin, Guid? replyId, CancellationToken cancellationToken = default);

    Task<(DiscussPhotoUploadStatus Status, IReadOnlyList<DiscussPhotoDto> Photos)> UploadPhotosAsync(
        DiscussPhotoOwner ownerType,
        Guid ownerId,
        Guid actingUserId,
        IReadOnlyList<DiscussPhotoUploadFile> files,
        CancellationToken cancellationToken = default);

    Task<DiscussOperationStatus> DeletePhotoAsync(Guid photoId, Guid actingUserId, CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType)?> GetPhotoContentAsync(Guid photoId, CancellationToken cancellationToken = default);

    Task<List<DiscussTagDto>> GetTagsAsync(bool curatedOnly, CancellationToken cancellationToken = default);
    Task<List<PopularTagDto>> GetPopularTagsAsync(int limit, CancellationToken cancellationToken = default);
    Task<DiscussStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteThreadAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<bool> DeleteReplyAsync(Guid replyId, CancellationToken cancellationToken = default);
    Task<DiscussThreadSummaryDto?> SetThreadFlagsAsync(
        Guid threadId, bool? isPinned, bool? isHot, CancellationToken cancellationToken = default);

    Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> CreateCuratedTagAsync(
        string name, string? slug, CancellationToken cancellationToken = default);
    Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> UpdateTagAsync(
        Guid tagId, string? name, string? slug, CancellationToken cancellationToken = default);
    Task<bool> DeleteTagAsync(Guid tagId, CancellationToken cancellationToken = default);
}
