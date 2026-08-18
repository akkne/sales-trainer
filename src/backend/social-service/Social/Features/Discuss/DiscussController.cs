using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Common.Extensions;
using Sellevate.Social.Features.Discuss.Constants;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Discuss.Services.Abstract;

namespace Sellevate.Social.Features.Discuss;

/// <summary>
/// The member-facing discussion feed. Phase 40.13: threads, replies, votes and photos are tenant
/// data, so <c>[TenantScoped]</c> makes the middleware refuse a request the gateway sent without an
/// organization rather than let it run unscoped — see <c>FriendController</c> for the same reasoning.
///
/// <para>
/// Only the photo-content route is anonymous, because an image tag cannot carry a bearer token; it
/// is guarded by the unguessability of the photo id instead, and hardened with the response headers
/// <see cref="GetPhotoContent"/> sets.
/// </para>
/// </summary>
[ApiController]
[Route("discuss")]
[TenantScoped]
[Authorize]
public sealed class DiscussController : ControllerBase
{
    private const string NoSniffContentTypeOptions = "nosniff";
    private const string DenyEverythingContentSecurityPolicy = "default-src 'none'";
    private const string DenyFrameOptions = "DENY";

    /// <summary>
    /// The content types the anonymous photo endpoint will echo back to a browser. Held as a static
    /// set so it is allocated once rather than per request, and compared case-insensitively because it
    /// is matched against a value stored at upload time.
    /// </summary>
    private static readonly HashSet<string> SafePhotoContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            DiscussPhotoConstants.PngContentType,
            DiscussPhotoConstants.JpegContentType,
            DiscussPhotoConstants.WebpContentType
        };

    private readonly IDiscussService _discussService;

    public DiscussController(IDiscussService discussService) => _discussService = discussService;

    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads(
        [FromQuery] string sort = DiscussSortOptions.Hot,
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DiscussFeedConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var viewerId = User.ResolveUserIdOrNull();
        if (viewerId == null) return Unauthorized();

        var query = new DiscussThreadQuery(sort, search, tag, page, pageSize, IncludeAll: false);
        var result = await _discussService.ListThreadsAsync(query, viewerId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("threads/{threadId:guid}")]
    public async Task<IActionResult> GetThread(Guid threadId, CancellationToken cancellationToken = default)
    {
        var viewerId = User.ResolveUserIdOrNull();
        if (viewerId == null) return Unauthorized();

        var thread = await _discussService.GetThreadAsync(threadId, viewerId.Value, incrementView: true, cancellationToken);
        if (thread == null) return NotFound(new { message = ErrorMessages.ThreadNotFound });
        return Ok(thread);
    }

    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequestDto request, CancellationToken cancellationToken = default)
    {
        var authorId = User.ResolveUserIdOrNull();
        if (authorId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = ErrorMessages.ThreadTitleAndBodyRequired });

        var thread = await _discussService.CreateThreadAsync(
            authorId.Value, request.Title, request.Body, request.Tags, cancellationToken);
        return CreatedAtAction(nameof(GetThread), new { threadId = thread.Id }, thread);
    }

    [HttpPost("threads/{threadId:guid}/replies")]
    public async Task<IActionResult> AddReply(Guid threadId, [FromBody] CreateReplyRequestDto request, CancellationToken cancellationToken = default)
    {
        var authorId = User.ResolveUserIdOrNull();
        if (authorId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = ErrorMessages.ReplyBodyRequired });

        var reply = await _discussService.AddReplyAsync(threadId, authorId.Value, request.Body, cancellationToken);
        if (reply == null) return NotFound(new { message = ErrorMessages.ThreadNotFound });
        return StatusCode(StatusCodes.Status201Created, reply);
    }

    [HttpPost("threads/{threadId:guid}/upvote")]
    public Task<IActionResult> UpvoteThread(Guid threadId, CancellationToken cancellationToken = default)
        => SetThreadVote(threadId, upvote: true, cancellationToken);

    [HttpDelete("threads/{threadId:guid}/upvote")]
    public Task<IActionResult> RemoveThreadUpvote(Guid threadId, CancellationToken cancellationToken = default)
        => SetThreadVote(threadId, upvote: false, cancellationToken);

    [HttpPost("replies/{replyId:guid}/upvote")]
    public Task<IActionResult> UpvoteReply(Guid replyId, CancellationToken cancellationToken = default)
        => SetReplyVote(replyId, upvote: true, cancellationToken);

    [HttpDelete("replies/{replyId:guid}/upvote")]
    public Task<IActionResult> RemoveReplyUpvote(Guid replyId, CancellationToken cancellationToken = default)
        => SetReplyVote(replyId, upvote: false, cancellationToken);

    [HttpPost("threads/{threadId:guid}/accepted-reply")]
    public async Task<IActionResult> SetAcceptedReply(Guid threadId, [FromBody] SetAcceptedReplyRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = User.ResolveUserIdOrNull();
        if (userId == null) return Unauthorized();

        var (status, thread) = await _discussService.SetAcceptedReplyAsync(
            threadId, userId.Value, IsAdmin(), request.ReplyId, cancellationToken);
        return MapAcceptedReplyResult(status, thread);
    }

    [HttpDelete("threads/{threadId:guid}/accepted-reply")]
    public async Task<IActionResult> ClearAcceptedReply(Guid threadId, CancellationToken cancellationToken = default)
    {
        var userId = User.ResolveUserIdOrNull();
        if (userId == null) return Unauthorized();

        var (status, thread) = await _discussService.SetAcceptedReplyAsync(
            threadId, userId.Value, IsAdmin(), replyId: null, cancellationToken);
        return MapAcceptedReplyResult(status, thread);
    }

    [HttpPost("threads/{threadId:guid}/photos")]
    [RequestSizeLimit(DiscussPhotoConstants.MaximumUploadRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DiscussPhotoConstants.MaximumUploadRequestSizeBytes)]
    public Task<IActionResult> UploadThreadPhotos(Guid threadId, [FromForm] IFormFileCollection files, CancellationToken cancellationToken = default)
        => UploadPhotos(DiscussPhotoOwner.Thread, threadId, files, cancellationToken);

    [HttpPost("replies/{replyId:guid}/photos")]
    [RequestSizeLimit(DiscussPhotoConstants.MaximumUploadRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DiscussPhotoConstants.MaximumUploadRequestSizeBytes)]
    public Task<IActionResult> UploadReplyPhotos(Guid replyId, [FromForm] IFormFileCollection files, CancellationToken cancellationToken = default)
        => UploadPhotos(DiscussPhotoOwner.Reply, replyId, files, cancellationToken);

    [HttpDelete("photos/{photoId:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid photoId, CancellationToken cancellationToken = default)
    {
        var userId = User.ResolveUserIdOrNull();
        if (userId == null) return Unauthorized();

        var status = await _discussService.DeletePhotoAsync(photoId, userId.Value, cancellationToken);
        return status switch
        {
            DiscussOperationStatus.Success => NoContent(),
            DiscussOperationStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = ErrorMessages.PhotoNotFound })
        };
    }

    /// <summary>
    /// Serves a photo's bytes to anyone holding the id, which is the only route here without a token.
    ///
    /// <para>
    /// A stored content type outside <see cref="SafePhotoContentTypes"/> is served as opaque binary
    /// rather than trusted, and the response carries the headers that stop a browser from sniffing a
    /// type of its own, framing the image, or executing anything it might contain. Both halves matter
    /// together: the allow-list decides what a browser is told, the headers decide what it may do if
    /// it disbelieves.
    /// </para>
    /// </summary>
    [HttpGet("photos/{photoId:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPhotoContent(Guid photoId, CancellationToken cancellationToken = default)
    {
        var content = await _discussService.GetPhotoContentAsync(photoId, cancellationToken);
        if (content == null) return NotFound();

        var servedContentType = SafePhotoContentTypes.Contains(content.Value.ContentType)
            ? content.Value.ContentType
            : MediaTypeNames.Application.Octet;

        Response.Headers[HeaderNames.CacheControl] = DiscussPhotoConstants.ContentCacheControl;
        Response.Headers[HeaderNames.XContentTypeOptions] = NoSniffContentTypeOptions;
        Response.Headers[HeaderNames.ContentSecurityPolicy] = DenyEverythingContentSecurityPolicy;
        Response.Headers[HeaderNames.XFrameOptions] = DenyFrameOptions;
        Response.Headers[HeaderNames.ContentDisposition] = $"inline; filename=\"photo-{photoId}.bin\"";

        return File(content.Value.Content, servedContentType);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags([FromQuery] bool curatedOnly = false, CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetTagsAsync(curatedOnly, cancellationToken));

    [HttpGet("tags/popular")]
    public async Task<IActionResult> GetPopularTags(
        [FromQuery] int limit = DiscussFeedConstants.DefaultPopularTagLimit,
        CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetPopularTagsAsync(limit, cancellationToken));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetStatsAsync(cancellationToken));

    private async Task<IActionResult> UploadPhotos(DiscussPhotoOwner ownerType, Guid ownerId, IFormFileCollection files, CancellationToken cancellationToken)
    {
        var userId = User.ResolveUserIdOrNull();
        if (userId == null) return Unauthorized();

        if (files == null || files.Count == 0)
            return BadRequest(new { message = ErrorMessages.PhotoFilesRequired });

        var uploadFiles = files
            .Select(file => new DiscussPhotoUploadFile(file.OpenReadStream(), file.FileName, file.Length))
            .ToList();

        var (status, photos) = await _discussService.UploadPhotosAsync(ownerType, ownerId, userId.Value, uploadFiles, cancellationToken);
        return status switch
        {
            DiscussPhotoUploadStatus.Success => Ok(new DiscussPhotoListDto(photos)),
            DiscussPhotoUploadStatus.OwnerNotFound => NotFound(new { message = ErrorMessages.PhotoOwnerNotFound }),
            DiscussPhotoUploadStatus.Forbidden => Forbid(),
            _ => BadRequest(new { message = ErrorMessages.PhotoValidationFailed })
        };
    }

    private async Task<IActionResult> SetThreadVote(Guid threadId, bool upvote, CancellationToken cancellationToken)
    {
        var userId = User.ResolveUserIdOrNull();
        if (userId == null) return Unauthorized();
        var result = await _discussService.SetThreadVoteAsync(threadId, userId.Value, upvote, cancellationToken);
        return result == null ? NotFound(new { message = ErrorMessages.ThreadNotFound }) : Ok(result);
    }

    private async Task<IActionResult> SetReplyVote(Guid replyId, bool upvote, CancellationToken cancellationToken)
    {
        var userId = User.ResolveUserIdOrNull();
        if (userId == null) return Unauthorized();
        var result = await _discussService.SetReplyVoteAsync(replyId, userId.Value, upvote, cancellationToken);
        return result == null ? NotFound(new { message = ErrorMessages.ReplyNotFound }) : Ok(result);
    }

    private IActionResult MapAcceptedReplyResult(DiscussOperationStatus status, DiscussThreadDetailDto? thread) =>
        status switch
        {
            DiscussOperationStatus.Success => Ok(thread),
            DiscussOperationStatus.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ErrorMessages.AcceptedReplyForbidden }),
            _ => NotFound(new { message = ErrorMessages.ThreadOrReplyNotFound })
        };

    /// <summary>
    /// Phase 40.6 audit: "can moderate any thread or reply" collapsed to the single remaining
    /// platform role when the global <c>Admin</c> role was removed.
    /// </summary>
    private bool IsAdmin() => User.IsInRole(AuthorizationPolicies.SuperAdministratorRole);
}
