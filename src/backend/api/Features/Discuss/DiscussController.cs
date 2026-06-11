using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Discuss.Models;
using SalesTrainer.Api.Features.Discuss.Services.Abstract;

namespace SalesTrainer.Api.Features.Discuss;

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
        CancellationToken ct = default)
    {
        var viewerId = GetUserId();
        if (viewerId == null) return Unauthorized();

        var query = new DiscussThreadQuery(sort, search, tag, page, pageSize, IncludeAll: false);
        var result = await _discussService.ListThreadsAsync(query, viewerId.Value, ct);
        return Ok(result);
    }

    [HttpGet("threads/{threadId:guid}")]
    public async Task<IActionResult> GetThread(Guid threadId, CancellationToken ct = default)
    {
        var viewerId = GetUserId();
        if (viewerId == null) return Unauthorized();

        var thread = await _discussService.GetThreadAsync(threadId, viewerId.Value, incrementView: true, ct);
        if (thread == null) return NotFound(new { message = "Thread not found" });
        return Ok(thread);
    }

    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequestDto request, CancellationToken ct = default)
    {
        var authorId = GetUserId();
        if (authorId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Title and body are required" });

        var thread = await _discussService.CreateThreadAsync(
            authorId.Value, request.Title, request.Body, request.Tags, ct);
        return CreatedAtAction(nameof(GetThread), new { threadId = thread.Id }, thread);
    }

    [HttpPost("threads/{threadId:guid}/replies")]
    public async Task<IActionResult> AddReply(Guid threadId, [FromBody] CreateReplyRequestDto request, CancellationToken ct = default)
    {
        var authorId = GetUserId();
        if (authorId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Body is required" });

        var reply = await _discussService.AddReplyAsync(threadId, authorId.Value, request.Body, ct);
        if (reply == null) return NotFound(new { message = "Thread not found" });
        return StatusCode(201, reply);
    }

    [HttpPost("threads/{threadId:guid}/upvote")]
    public Task<IActionResult> UpvoteThread(Guid threadId, CancellationToken ct = default)
        => SetThreadVote(threadId, upvote: true, ct);

    [HttpDelete("threads/{threadId:guid}/upvote")]
    public Task<IActionResult> RemoveThreadUpvote(Guid threadId, CancellationToken ct = default)
        => SetThreadVote(threadId, upvote: false, ct);

    [HttpPost("replies/{replyId:guid}/upvote")]
    public Task<IActionResult> UpvoteReply(Guid replyId, CancellationToken ct = default)
        => SetReplyVote(replyId, upvote: true, ct);

    [HttpDelete("replies/{replyId:guid}/upvote")]
    public Task<IActionResult> RemoveReplyUpvote(Guid replyId, CancellationToken ct = default)
        => SetReplyVote(replyId, upvote: false, ct);

    [HttpPost("threads/{threadId:guid}/accepted-reply")]
    public async Task<IActionResult> SetAcceptedReply(Guid threadId, [FromBody] SetAcceptedReplyRequestDto request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (status, thread) = await _discussService.SetAcceptedReplyAsync(
            threadId, userId.Value, IsAdmin(), request.ReplyId, ct);
        return MapAcceptedReplyResult(status, thread);
    }

    [HttpDelete("threads/{threadId:guid}/accepted-reply")]
    public async Task<IActionResult> ClearAcceptedReply(Guid threadId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (status, thread) = await _discussService.SetAcceptedReplyAsync(
            threadId, userId.Value, IsAdmin(), replyId: null, ct);
        return MapAcceptedReplyResult(status, thread);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags([FromQuery] bool curatedOnly = false, CancellationToken ct = default)
        => Ok(await _discussService.GetTagsAsync(curatedOnly, ct));

    [HttpGet("tags/popular")]
    public async Task<IActionResult> GetPopularTags([FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _discussService.GetPopularTagsAsync(limit, ct));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
        => Ok(await _discussService.GetStatsAsync(ct));

    private async Task<IActionResult> SetThreadVote(Guid threadId, bool upvote, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _discussService.SetThreadVoteAsync(threadId, userId.Value, upvote, ct);
        return result == null ? NotFound(new { message = "Thread not found" }) : Ok(result);
    }

    private async Task<IActionResult> SetReplyVote(Guid replyId, bool upvote, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _discussService.SetReplyVoteAsync(replyId, userId.Value, upvote, ct);
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
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
