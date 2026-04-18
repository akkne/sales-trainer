using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Friends.Models;
using SalesTrainer.Api.Features.Friends.Services.Abstract;

namespace SalesTrainer.Api.Features.Friends;

[ApiController]
[Route("friends")]
[Authorize]
public class FriendController(IFriendService friendService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FriendDto>>> GetFriends(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var friends = await friendService.GetFriendsAsync(userId, cancellationToken);
        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetPendingRequests(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var pendingRequests = await friendService.GetPendingRequestsAsync(userId, cancellationToken);
        return Ok(pendingRequests);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> SendFriendRequest(
        [FromBody] SendFriendRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var friendship = await friendService.SendFriendRequestAsync(userId, request.AddresseeId, cancellationToken);
            return Created($"/friends/requests/{friendship.Id}", new { friendshipId = friendship.Id });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPut("requests/{friendshipId:guid}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(
        Guid friendshipId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            await friendService.AcceptFriendRequestAsync(userId, friendshipId, cancellationToken);
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

    [HttpPut("requests/{friendshipId:guid}/decline")]
    public async Task<IActionResult> DeclineFriendRequest(
        Guid friendshipId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            await friendService.DeclineFriendRequestAsync(userId, friendshipId, cancellationToken);
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

    [HttpDelete("{friendUserId:guid}")]
    public async Task<IActionResult> RemoveFriend(
        Guid friendUserId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            await friendService.RemoveFriendAsync(userId, friendUserId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<UserSearchResultDto>>> SearchUsers(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var searchResults = await friendService.SearchUsersAsync(userId, query, cancellationToken);
        return Ok(searchResults);
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<FriendLeaderboardEntryDto>>> GetFriendLeaderboard(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var leaderboardEntries = await friendService.GetFriendLeaderboardAsync(userId, cancellationToken);
        return Ok(leaderboardEntries);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<List<FriendActivityDto>>> GetFriendActivityFeed(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var activityFeed = await friendService.GetFriendActivityFeedAsync(userId, cancellationToken: cancellationToken);
        return Ok(activityFeed);
    }

    [HttpGet("profile/{targetUserId:guid}")]
    public async Task<ActionResult<PublicProfileDto>> GetPublicProfile(
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var publicProfile = await friendService.GetPublicProfileAsync(userId, targetUserId, cancellationToken);
            return Ok(publicProfile);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(rawUserId, out userId);
    }
}
