using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Discuss.Services.Abstract;

namespace Sellevate.Social.Features.Discuss;

[ApiController]
[Route("admin/discuss")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminDiscussController : ControllerBase
{
    private readonly IDiscussService _discussService;
    private readonly ILogger<AdminDiscussController> _logger;

    public AdminDiscussController(IDiscussService discussService, ILogger<AdminDiscussController> logger)
    {
        _discussService = discussService;
        _logger = logger;
    }

    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var viewerId = GetUserId() ?? Guid.Empty;
        var query = new DiscussThreadQuery("new", search, Tag: null, page, pageSize, IncludeAll: true);
        var result = await _discussService.ListThreadsAsync(query, viewerId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("threads/{threadId:guid}")]
    public async Task<IActionResult> DeleteThread(Guid threadId, CancellationToken cancellationToken = default)
    {
        var deleted = await _discussService.DeleteThreadAsync(threadId, cancellationToken);
        if (!deleted) return NotFound(new { message = "Thread not found" });
        _logger.LogInformation("Admin deleted discuss thread {ThreadId}", threadId);
        return NoContent();
    }

    [HttpPost("threads/{threadId:guid}/pin")]
    public async Task<IActionResult> SetPin(Guid threadId, [FromBody] SetPinRequestDto request, CancellationToken cancellationToken = default)
    {
        var summary = await _discussService.SetThreadFlagsAsync(threadId, request.IsPinned, isHot: null, cancellationToken);
        if (summary == null) return NotFound(new { message = "Thread not found" });
        _logger.LogInformation("Admin set pin={IsPinned} on discuss thread {ThreadId}", request.IsPinned, threadId);
        return Ok(summary);
    }

    [HttpPost("threads/{threadId:guid}/hot")]
    public async Task<IActionResult> SetHot(Guid threadId, [FromBody] SetHotRequestDto request, CancellationToken cancellationToken = default)
    {
        var summary = await _discussService.SetThreadFlagsAsync(threadId, isPinned: null, request.IsHot, cancellationToken);
        if (summary == null) return NotFound(new { message = "Thread not found" });
        _logger.LogInformation("Admin set hot={IsHot} on discuss thread {ThreadId}", request.IsHot, threadId);
        return Ok(summary);
    }

    [HttpDelete("replies/{replyId:guid}")]
    public async Task<IActionResult> DeleteReply(Guid replyId, CancellationToken cancellationToken = default)
    {
        var deleted = await _discussService.DeleteReplyAsync(replyId, cancellationToken);
        if (!deleted) return NotFound(new { message = "Reply not found" });
        _logger.LogInformation("Admin deleted discuss reply {ReplyId}", replyId);
        return NoContent();
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags(CancellationToken cancellationToken = default)
        => Ok(await _discussService.GetTagsAsync(curatedOnly: false, cancellationToken));

    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });

        var (status, tag) = await _discussService.CreateCuratedTagAsync(request.Name, request.Slug, cancellationToken);
        if (status == DiscussOperationStatus.Conflict)
            return Conflict(new { message = "A tag with this slug already exists" });

        _logger.LogInformation("Admin created discuss tag {TagId}: {Name}", tag!.Id, tag.Name);
        return StatusCode(201, tag);
    }

    [HttpPut("tags/{tagId:guid}")]
    public async Task<IActionResult> UpdateTag(Guid tagId, [FromBody] UpdateTagRequestDto request, CancellationToken cancellationToken = default)
    {
        var (status, tag) = await _discussService.UpdateTagAsync(tagId, request.Name, request.Slug, cancellationToken);
        return status switch
        {
            DiscussOperationStatus.Success => Ok(tag),
            DiscussOperationStatus.Conflict => Conflict(new { message = "A tag with this slug already exists" }),
            _ => NotFound(new { message = "Tag not found" })
        };
    }

    [HttpDelete("tags/{tagId:guid}")]
    public async Task<IActionResult> DeleteTag(Guid tagId, CancellationToken cancellationToken = default)
    {
        var deleted = await _discussService.DeleteTagAsync(tagId, cancellationToken);
        if (!deleted) return NotFound(new { message = "Tag not found" });
        _logger.LogInformation("Admin deleted discuss tag {TagId}", tagId);
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var identifier) ? identifier : null;
    }
}
