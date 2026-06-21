using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Social.Features.Discuss.Constants;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Discuss.Services.Abstract;

namespace Sellevate.Social.Features.Discuss;

[ApiController]
[Route("discuss")]
[Authorize]
public sealed class DiscussController : ControllerBase
{
    private readonly IDiscussService _discussService;

    public DiscussController(IDiscussService discussService) => _discussService = discussService;

    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads(
        [FromQuery] string sort = "hot",
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var viewerId = GetUserId();
        if (viewerId == null) return Unauthorized();

        var query = new DiscussThreadQuery(sort, search, tag, page, pageSize, IncludeAll: false);
        var result = await _discussService.ListThreadsAsync(query, viewerId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("threads/{threadId:guid}")]
    public async Task<IActionResult> GetThread(Guid threadId, CancellationToken cancellationToken = default)
    {
        var viewerId = GetUserId();
        if (viewerId == null) return Unauthorized();

        var thread = await _discussService.GetThreadAsync(threadId, viewerId.Value, incrementView: true, cancellationToken);
        if (thread == null) return NotFound(new { message = "Thread not found" });
        return Ok(thread);
    }

    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequestDto request, CancellationToken cancellationToken = default)
    {
        var authorId = GetUserId();
        if (authorId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Title and body are required" });

        var thread = await _discussService.CreateThreadAsync(
            authorId.Value, request.Title, request.Body, request.Tags, cancellationToken);
        return CreatedAtAction(nameof(GetThread), new { threadId = thread.Id }, thread);
    }

    [HttpPost("threads/{threadId:guid}/replies")]
    public async Task<IActionResult> AddReply(Guid threadId, [FromBody] CreateReplyRequestDto request, CancellationToken cancellationToken = default)
    {
        var authorId = GetUserId();
        if (authorId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Body is required" });

        var reply = await _discussService.AddReplyAsync(threadId, authorId.Value, request.Body, cancellationToken);
        if (reply == null) return NotFound(new { message = "Thread not found" });
        return StatusCode(201, reply);
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (status, thread) = await _discussService.SetAcceptedReplyAsync(
            threadId, userId.Value, IsAdmin(), request.ReplyId, cancellationToken);
        return MapAcceptedReplyResult(status, thread);
    }

    [HttpDelete("threads/{threadId:guid}/accepted-reply")]
    public async Task<IActionResult> ClearAcceptedReply(Guid threadId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var status = await _discussService.DeletePhotoAsync(photoId, userId.Value, cancellationToken);
        return status switch
        {
            DiscussOperationStatus.Success => NoContent(),
            DiscussOperationStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = "Photo not found" })
        };
    }

    [HttpGet("photos/{photoId:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPhotoContent(Guid photoId, CancellationToken cancellationToken = default)
    {
        var content = await _discussService.GetPhotoContentAsync(photoId, cancellationToken);
        if (content == null) return NotFound();

        // Ensure we only serve with a known-safe image content-type.
        // Anything not in the allow-list is served as opaque binary to prevent MIME sniffing exploitation.
        var safeContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/webp"
        };
        var servedContentType = safeContentTypes.Contains(content.Value.ContentType)
            ? content.Value.ContentType
            : "application/octet-stream";

        // Defense-in-depth headers for this anonymous, public endpoint.
        Response.Headers["Cache-Control"] = "public, max-age=60";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        Response.Headers["X-Frame-Options"] = "DENY";
        Response.Headers["Content-Disposition"] = $"inline; filename=\"photo-{photoId}.bin\"";

        return File(content.Value.Content, servedContentType);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags([FromQuery] bool curatedOnly = false, CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetTagsAsync(curatedOnly, cancellationToken));

    [HttpGet("tags/popular")]
    public async Task<IActionResult> GetPopularTags([FromQuery] int limit = 10, CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetPopularTagsAsync(limit, cancellationToken));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetStatsAsync(cancellationToken));

    private async Task<IActionResult> UploadPhotos(DiscussPhotoOwner ownerType, Guid ownerId, IFormFileCollection files, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (files == null || files.Count == 0)
            return BadRequest(new { message = "No files were provided" });

        var uploadFiles = files
            .Select(file => new DiscussPhotoUploadFile(file.OpenReadStream(), file.FileName, file.Length))
            .ToList();

        var (status, photos) = await _discussService.UploadPhotosAsync(ownerType, ownerId, userId.Value, uploadFiles, cancellationToken);
        return status switch
        {
            DiscussPhotoUploadStatus.Success => Ok(new DiscussPhotoListDto(photos)),
            DiscussPhotoUploadStatus.OwnerNotFound => NotFound(new { message = "Owner not found" }),
            DiscussPhotoUploadStatus.Forbidden => Forbid(),
            _ => BadRequest(new { message = "One or more photos failed validation" })
        };
    }

    private async Task<IActionResult> SetThreadVote(Guid threadId, bool upvote, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _discussService.SetThreadVoteAsync(threadId, userId.Value, upvote, cancellationToken);
        return result == null ? NotFound(new { message = "Thread not found" }) : Ok(result);
    }

    private async Task<IActionResult> SetReplyVote(Guid replyId, bool upvote, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _discussService.SetReplyVoteAsync(replyId, userId.Value, upvote, cancellationToken);
        return result == null ? NotFound(new { message = "Reply not found" }) : Ok(result);
    }

    private IActionResult MapAcceptedReplyResult(DiscussOperationStatus status, DiscussThreadDetailDto? thread) =>
        status switch
        {
            DiscussOperationStatus.Success => Ok(thread),
            DiscussOperationStatus.Forbidden => StatusCode(403, new { message = "Only the thread author or an admin can do this" }),
            _ => NotFound(new { message = "Thread or reply not found" })
        };

    private bool IsAdmin() => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var identifier) ? identifier : null;
    }
}
