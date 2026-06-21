using Sellevate.Social.Features.Friends.Models;

namespace Sellevate.Social.Features.Friends.Services.Abstract;

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
