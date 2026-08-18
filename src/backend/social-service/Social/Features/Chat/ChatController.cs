using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Common.Extensions;
using Sellevate.Social.Features.Chat.Constants;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;

namespace Sellevate.Social.Features.Chat;

/// <summary>
/// One-to-one chat between accepted friends. Translates the service's exceptions into status codes
/// and nothing else — no rule about who may read a conversation lives here, because the repository
/// filter that decides it is the tenant boundary.
///
/// <para>
/// Phase 40.13. <c>[TenantScoped]</c> makes the middleware answer 403 when the gateway supplied no
/// <c>X-Organization-Id</c>, instead of letting the request run with no tenant — where the Mongo
/// repository would throw a 500 and the Postgres reads would silently see nothing.
/// </para>
/// </summary>
[ApiController]
[Route("chat")]
[TenantScoped]
[Authorize]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<List<ChatConversationSummaryDto>>> GetConversations(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var conversations = await chatService.GetConversationListAsync(userId, cancellationToken);
        return Ok(conversations);
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ChatConversationSummaryDto>> GetOrCreateConversation(
        [FromBody] CreateConversationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
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
        [FromQuery] int limit = ChatConstants.DefaultMessagePageSize,
        [FromQuery] string? before = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId))
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
        if (!User.TryResolveUserId(out var userId))
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

    [HttpPost("conversations/{conversationId}/read")]
    public async Task<IActionResult> MarkConversationRead(
        string conversationId,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        try
        {
            await chatService.MarkConversationReadAsync(userId, conversationId, cancellationToken);
            return NoContent();
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
}
