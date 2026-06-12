using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Services.Abstract;

public interface IDiscussService
{
    // ---- User-facing ----
    Task<PagedResultDto<DiscussThreadSummaryDto>> ListThreadsAsync(
        DiscussThreadQuery query, Guid viewerId, CancellationToken ct = default);

    Task<DiscussThreadDetailDto?> GetThreadAsync(
        Guid threadId, Guid viewerId, bool incrementView, CancellationToken ct = default);

    Task<DiscussThreadDetailDto> CreateThreadAsync(
        Guid authorId, string title, string body, IReadOnlyList<string> tagLabels, CancellationToken ct = default);

    /// <summary>Returns null if the thread does not exist.</summary>
    Task<DiscussReplyDto?> AddReplyAsync(
        Guid threadId, Guid authorId, string body, CancellationToken ct = default);

    /// <summary>Returns null if the target does not exist.</summary>
    Task<VoteResultDto?> SetThreadVoteAsync(Guid threadId, Guid userId, bool upvote, CancellationToken ct = default);
    Task<VoteResultDto?> SetReplyVoteAsync(Guid replyId, Guid userId, bool upvote, CancellationToken ct = default);

    /// <summary>Marks (replyId set) or clears (replyId null) the accepted reply. Author or admin only.</summary>
    Task<(DiscussOperationStatus Status, DiscussThreadDetailDto? Thread)> SetAcceptedReplyAsync(
        Guid threadId, Guid actingUserId, bool isAdmin, Guid? replyId, CancellationToken ct = default);

    Task<(DiscussPhotoUploadStatus Status, IReadOnlyList<DiscussPhotoDto> Photos)> UploadPhotosAsync(
        DiscussPhotoOwner ownerType,
        Guid ownerId,
        Guid actingUserId,
        IReadOnlyList<DiscussPhotoUploadFile> files,
        CancellationToken ct = default);

    Task<DiscussOperationStatus> DeletePhotoAsync(Guid photoId, Guid actingUserId, CancellationToken ct = default);

    Task<(Stream Content, string ContentType)?> GetPhotoContentAsync(Guid photoId, CancellationToken ct = default);

    Task<List<DiscussTagDto>> GetTagsAsync(bool curatedOnly, CancellationToken ct = default);
    Task<List<PopularTagDto>> GetPopularTagsAsync(int limit, CancellationToken ct = default);
    Task<DiscussStatsDto> GetStatsAsync(CancellationToken ct = default);

    // ---- Admin ----
    Task<bool> DeleteThreadAsync(Guid threadId, CancellationToken ct = default);
    Task<bool> DeleteReplyAsync(Guid replyId, CancellationToken ct = default);
    Task<DiscussThreadSummaryDto?> SetThreadFlagsAsync(
        Guid threadId, bool? isPinned, bool? isHot, CancellationToken ct = default);

    Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> CreateCuratedTagAsync(
        string name, string? slug, CancellationToken ct = default);
    Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> UpdateTagAsync(
        Guid tagId, string? name, string? slug, CancellationToken ct = default);
    Task<bool> DeleteTagAsync(Guid tagId, CancellationToken ct = default);
}
