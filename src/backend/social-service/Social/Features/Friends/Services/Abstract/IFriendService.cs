using Sellevate.Social.Features.Friends.Models;

namespace Sellevate.Social.Features.Friends.Services.Abstract;

/// <summary>
/// Friend requests, friendships and the profile and search screens built on them. A friendship is
/// tenant data, so every method here answers within the caller's organization only; user search is
/// the one exception and reads the platform-wide identity directory, bounded by
/// <see cref="Constants.FriendSearchConstants"/>.
///
/// <para>
/// State transitions are asymmetric on purpose: only the addressee may accept or decline, only the
/// requester may cancel, a declined pair can be revived by a later request, and a cancelled one is
/// erased. Callers get an exception per refusal rather than a status, and the controller maps
/// <see cref="KeyNotFoundException"/> to 404 and <see cref="InvalidOperationException"/> to 400.
/// </para>
/// </summary>
public interface IFriendService
{
    Task<List<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<FriendRequestDto>> GetPendingRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Friendship> SendFriendRequestAsync(Guid requesterId, Guid addresseeId, CancellationToken cancellationToken = default);
    Task AcceptFriendRequestAsync(Guid userId, Guid friendshipId, CancellationToken cancellationToken = default);
    Task DeclineFriendRequestAsync(Guid userId, Guid friendshipId, CancellationToken cancellationToken = default);
    Task CancelFriendRequestAsync(Guid userId, Guid friendshipId, CancellationToken cancellationToken = default);
    Task RemoveFriendAsync(Guid userId, Guid friendUserId, CancellationToken cancellationToken = default);
    Task<List<UserSearchResultDto>> SearchUsersAsync(Guid currentUserId, string query, CancellationToken cancellationToken = default);
    Task<PublicProfileDto> GetPublicProfileAsync(Guid viewerUserId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<List<FriendLeaderboardEntryDto>> GetFriendLeaderboardAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<FriendActivityDto>> GetFriendActivityFeedAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default);
}
