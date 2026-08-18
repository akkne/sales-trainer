using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Common.Extensions;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Features.Friends.Services.Abstract;

namespace Sellevate.Social.Features.Friends;

/// <summary>
/// Friend requests, the friend list, user search and public profiles. Business rules live in
/// <see cref="IFriendService"/>; this maps its exceptions to status codes —
/// <see cref="KeyNotFoundException"/> to 404, <see cref="InvalidOperationException"/> to 400.
///
/// <para>
/// Phase 40.13. Friendships are tenant data, and without an organization there is no correct answer
/// to any route here, so <c>[TenantScoped]</c> makes the middleware refuse the request rather than
/// serve an empty list that reads as "you have no friends".
/// </para>
/// </summary>
[ApiController]
[Route("friends")]
[TenantScoped]
[Authorize]
public sealed class FriendController(IFriendService friendService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FriendDto>>> GetFriends(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var friends = await friendService.GetFriendsAsync(userId, cancellationToken);
        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetPendingRequests(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var pendingRequests = await friendService.GetPendingRequestsAsync(userId, cancellationToken);
        return Ok(pendingRequests);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> SendFriendRequest(
        [FromBody] SendFriendRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
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
        if (!User.TryResolveUserId(out var userId))
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
        if (!User.TryResolveUserId(out var userId))
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

    [HttpDelete("requests/{friendshipId:guid}")]
    public async Task<IActionResult> CancelFriendRequest(
        Guid friendshipId,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        try
        {
            await friendService.CancelFriendRequestAsync(userId, friendshipId, cancellationToken);
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
        if (!User.TryResolveUserId(out var userId))
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
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var searchResults = await friendService.SearchUsersAsync(userId, query, cancellationToken);
        return Ok(searchResults);
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<FriendLeaderboardEntryDto>>> GetFriendLeaderboard(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var leaderboardEntries = await friendService.GetFriendLeaderboardAsync(userId, cancellationToken);
        return Ok(leaderboardEntries);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<List<FriendActivityDto>>> GetFriendActivityFeed(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var activityFeed = await friendService.GetFriendActivityFeedAsync(userId, cancellationToken: cancellationToken);
        return Ok(activityFeed);
    }

    [HttpGet("profile/{targetUserId:guid}")]
    public async Task<ActionResult<PublicProfileDto>> GetPublicProfile(
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
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
}
