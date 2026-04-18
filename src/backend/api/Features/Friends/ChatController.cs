using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Friends.Models;
using SalesTrainer.Api.Features.Friends.Services.Abstract;

namespace SalesTrainer.Api.Features.Friends;

[ApiController]
[Route("chat")]
[Authorize]
public class ChatController(IChatService chatService) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<List<ChatConversationSummaryDto>>> GetConversations(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var conversations = await chatService.GetConversationListAsync(userId, cancellationToken);
        return Ok(conversations);
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ChatConversationSummaryDto>> GetOrCreateConversation(
        [FromBody] CreateConversationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var conversation = await chatService.GetOrCreateConversationAsync(userId, request.FriendUserId, cancellationToken);
            return Ok(conversation);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(
        string conversationId,
        [FromQuery] int limit = 50,
        [FromQuery] string? before = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var messages = await chatService.GetMessagesAsync(userId, conversationId, limit, before, cancellationToken);
            return Ok(messages);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("conversations/{conversationId}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        string conversationId,
        [FromBody] SendChatMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var message = await chatService.SendMessageAsync(userId, conversationId, request.Content, cancellationToken);
            return Ok(message);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(rawUserId, out userId);
    }
}
