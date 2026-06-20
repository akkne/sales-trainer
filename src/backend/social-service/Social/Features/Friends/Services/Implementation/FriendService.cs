using Microsoft.EntityFrameworkCore;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Features.Friends.Services.Abstract;
using Sellevate.Social.Identity;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Features.Friends.Services.Implementation;

internal sealed class FriendService(
    SocialDbContext databaseContext,
    ISocialEventPublisher eventPublisher) : IFriendService
{
    private const int MaximumSearchResultCount = 20;
    private const string DirectionIncoming = "incoming";
    private const string DirectionOutgoing = "outgoing";
    private const string StatusNone = "none";
    private const string StatusPendingOutgoing = "pending_outgoing";
    private const string StatusPendingIncoming = "pending_incoming";
    private const string StatusFriends = "friends";
    private const string UnknownDisplayName = "Пользователь";

    public async Task<List<FriendDto>> GetFriendsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var acceptedFriendships = await databaseContext.Friendships
            .Where(friendship =>
                friendship.Status == FriendshipStatus.Accepted &&
                (friendship.RequesterId == userId || friendship.AddresseeId == userId))
            .ToListAsync(cancellationToken);

        var friendUserIds = acceptedFriendships
            .Select(friendship => friendship.RequesterId == userId
                ? friendship.AddresseeId
                : friendship.RequesterId)
            .Distinct()
            .ToList();

        if (friendUserIds.Count == 0)
            return [];

        var friendReplicas = await GetReplicasAsync(friendUserIds, cancellationToken);

        return friendReplicas
            .Select(replica => new FriendDto(
                replica.UserId,
                replica.DisplayName,
                null,
                0,
                0,
                0,
                AvatarUrls.For(replica.UserId)))
            .OrderByDescending(friend => friend.TotalXpAmount)
            .ToList();
    }

    public async Task<List<FriendRequestDto>> GetPendingRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var pendingFriendships = await databaseContext.Friendships
            .Where(friendship =>
                friendship.Status == FriendshipStatus.Pending &&
                (friendship.RequesterId == userId || friendship.AddresseeId == userId))
            .ToListAsync(cancellationToken);

        var involvedUserIds = pendingFriendships
            .SelectMany(friendship => new[] { friendship.RequesterId, friendship.AddresseeId })
            .Where(identifier => identifier != userId)
            .Distinct()
            .ToList();

        var involvedReplicas = await GetReplicaMapAsync(involvedUserIds, cancellationToken);

        return pendingFriendships
            .Select(friendship =>
            {
                var isIncoming = friendship.AddresseeId == userId;
                var otherUserId = isIncoming ? friendship.RequesterId : friendship.AddresseeId;
                var otherDisplayName = involvedReplicas.GetValueOrDefault(otherUserId, UnknownDisplayName);

                return new FriendRequestDto(
                    friendship.Id,
                    otherUserId,
                    otherDisplayName,
                    null,
                    isIncoming ? DirectionIncoming : DirectionOutgoing,
                    friendship.CreatedAt);
            })
            .OrderByDescending(request => request.CreatedAt)
            .ToList();
    }

    public async Task<Friendship> SendFriendRequestAsync(
        Guid requesterId,
        Guid addresseeId,
        CancellationToken cancellationToken = default)
    {
        if (requesterId == addresseeId)
            throw new InvalidOperationException("Cannot send a friend request to yourself.");

        var addresseeExists = await databaseContext.UserReplicas
            .AnyAsync(replica => replica.UserId == addresseeId, cancellationToken);

        if (!addresseeExists)
            throw new KeyNotFoundException("Target user not found.");

        var existingFriendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                (friendship.RequesterId == requesterId && friendship.AddresseeId == addresseeId) ||
                (friendship.RequesterId == addresseeId && friendship.AddresseeId == requesterId),
                cancellationToken);

        if (existingFriendship is not null)
        {
            if (existingFriendship.Status == FriendshipStatus.Accepted)
                throw new InvalidOperationException("You are already friends with this user.");

            if (existingFriendship.Status == FriendshipStatus.Pending)
                throw new InvalidOperationException("A friend request already exists between you and this user.");

            if (existingFriendship.Status == FriendshipStatus.Declined)
            {
                existingFriendship.RequesterId = requesterId;
                existingFriendship.AddresseeId = addresseeId;
                existingFriendship.Status = FriendshipStatus.Pending;
                existingFriendship.CreatedAt = DateTime.UtcNow;
                existingFriendship.AcceptedAt = null;
                await databaseContext.SaveChangesAsync(cancellationToken);

                await PublishFriendRequestReceivedAsync(existingFriendship, cancellationToken);

                return existingFriendship;
            }
        }

        var friendship = new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.Friendships.Add(friendship);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await PublishFriendRequestReceivedAsync(friendship, cancellationToken);

        return friendship;
    }

    public async Task AcceptFriendRequestAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken = default)
    {
        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship => friendship.Id == friendshipId, cancellationToken)
            ?? throw new KeyNotFoundException("Friend request not found.");

        if (friendship.AddresseeId != userId)
            throw new InvalidOperationException("Only the addressee can accept a friend request.");

        if (friendship.Status != FriendshipStatus.Pending)
            throw new InvalidOperationException("This friend request is no longer pending.");

        friendship.Status = FriendshipStatus.Accepted;
        friendship.AcceptedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);

        await PublishFriendRequestAcceptedAsync(friendship, cancellationToken);
    }

    public async Task DeclineFriendRequestAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken = default)
    {
        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship => friendship.Id == friendshipId, cancellationToken)
            ?? throw new KeyNotFoundException("Friend request not found.");

        if (friendship.AddresseeId != userId)
            throw new InvalidOperationException("Only the addressee can decline a friend request.");

        if (friendship.Status != FriendshipStatus.Pending)
            throw new InvalidOperationException("This friend request is no longer pending.");

        friendship.Status = FriendshipStatus.Declined;
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFriendAsync(
        Guid userId,
        Guid friendUserId,
        CancellationToken cancellationToken = default)
    {
        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                friendship.Status == FriendshipStatus.Accepted &&
                ((friendship.RequesterId == userId && friendship.AddresseeId == friendUserId) ||
                 (friendship.RequesterId == friendUserId && friendship.AddresseeId == userId)),
                cancellationToken)
            ?? throw new KeyNotFoundException("Friendship not found.");

        databaseContext.Friendships.Remove(friendship);
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(
        Guid currentUserId,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        var searchPattern = $"%{query}%";

        var matchingReplicas = await databaseContext.UserReplicas
            .Where(replica =>
                replica.UserId != currentUserId &&
                (EF.Functions.ILike(replica.DisplayName, searchPattern) ||
                 EF.Functions.ILike(replica.Email, searchPattern)))
            .Take(MaximumSearchResultCount)
            .ToListAsync(cancellationToken);

        if (matchingReplicas.Count == 0)
            return [];

        var matchingUserIds = matchingReplicas.Select(replica => replica.UserId).ToList();

        var existingFriendships = await databaseContext.Friendships
            .Where(friendship =>
                (friendship.RequesterId == currentUserId && matchingUserIds.Contains(friendship.AddresseeId)) ||
                (matchingUserIds.Contains(friendship.RequesterId) && friendship.AddresseeId == currentUserId))
            .ToListAsync(cancellationToken);

        return matchingReplicas
            .Select(replica =>
            {
                var friendship = existingFriendships.FirstOrDefault(friendship =>
                    (friendship.RequesterId == currentUserId && friendship.AddresseeId == replica.UserId) ||
                    (friendship.RequesterId == replica.UserId && friendship.AddresseeId == currentUserId));

                var friendshipStatus = ResolveFriendshipStatus(friendship, currentUserId);

                return new UserSearchResultDto(
                    replica.UserId,
                    replica.DisplayName,
                    null,
                    friendshipStatus,
                    AvatarUrls.For(replica.UserId));
            })
            .ToList();
    }

    public async Task<PublicProfileDto> GetPublicProfileAsync(
        Guid viewerUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var targetReplica = await databaseContext.UserReplicas
            .FirstOrDefaultAsync(replica => replica.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        var existingFriendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                (friendship.RequesterId == viewerUserId && friendship.AddresseeId == targetUserId) ||
                (friendship.RequesterId == targetUserId && friendship.AddresseeId == viewerUserId),
                cancellationToken);

        var friendshipStatus = viewerUserId == targetUserId
            ? StatusNone
            : ResolveFriendshipStatus(existingFriendship, viewerUserId);

        return new PublicProfileDto(
            targetReplica.UserId,
            targetReplica.DisplayName,
            null,
            0,
            0,
            0,
            0.0,
            friendshipStatus,
            AvatarUrls.For(targetReplica.UserId));
    }

    public async Task<List<FriendLeaderboardEntryDto>> GetFriendLeaderboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var friendUserIds = await GetAcceptedFriendUserIdsAsync(userId, cancellationToken);
        var allParticipantIds = friendUserIds.Append(userId).Distinct().ToList();

        var participantReplicas = await GetReplicaMapAsync(allParticipantIds, cancellationToken);

        return allParticipantIds
            .Select((participantId, index) => new FriendLeaderboardEntryDto(
                participantId,
                participantReplicas.GetValueOrDefault(participantId, UnknownDisplayName),
                0,
                index + 1,
                participantId == userId,
                AvatarUrls.For(participantId)))
            .ToList();
    }

    public async Task<List<FriendActivityDto>> GetFriendActivityFeedAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        await GetAcceptedFriendUserIdsAsync(userId, cancellationToken);
        return [];
    }

    private async Task<List<Guid>> GetAcceptedFriendUserIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var acceptedFriendships = await databaseContext.Friendships
            .Where(friendship =>
                friendship.Status == FriendshipStatus.Accepted &&
                (friendship.RequesterId == userId || friendship.AddresseeId == userId))
            .ToListAsync(cancellationToken);

        return acceptedFriendships
            .Select(friendship => friendship.RequesterId == userId
                ? friendship.AddresseeId
                : friendship.RequesterId)
            .Distinct()
            .ToList();
    }

    private async Task<List<UserReplica>> GetReplicasAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        return await databaseContext.UserReplicas
            .Where(replica => userIds.Contains(replica.UserId))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> GetReplicaMapAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        return await databaseContext.UserReplicas
            .Where(replica => userIds.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);
    }

    private async Task PublishFriendRequestReceivedAsync(
        Friendship friendship,
        CancellationToken cancellationToken)
    {
        var requesterDisplayName = await ResolveDisplayNameAsync(friendship.RequesterId, cancellationToken);

        await eventPublisher.PublishFriendRequestReceivedAsync(
            new FriendRequestReceivedEvent(
                friendship.AddresseeId,
                requesterDisplayName,
                friendship.RequesterId,
                friendship.Id),
            cancellationToken);
    }

    private async Task PublishFriendRequestAcceptedAsync(
        Friendship friendship,
        CancellationToken cancellationToken)
    {
        var addresseeDisplayName = await ResolveDisplayNameAsync(friendship.AddresseeId, cancellationToken);

        await eventPublisher.PublishFriendRequestAcceptedAsync(
            new FriendRequestAcceptedEvent(
                friendship.RequesterId,
                addresseeDisplayName,
                friendship.AddresseeId),
            cancellationToken);
    }

    private async Task<string> ResolveDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var displayName = await databaseContext.UserReplicas
            .Where(replica => replica.UserId == userId)
            .Select(replica => replica.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(displayName) ? UnknownDisplayName : displayName;
    }

    private static string ResolveFriendshipStatus(Friendship? friendship, Guid currentUserId)
    {
        if (friendship is null)
            return StatusNone;

        return friendship.Status switch
        {
            FriendshipStatus.Accepted => StatusFriends,
            FriendshipStatus.Pending when friendship.RequesterId == currentUserId => StatusPendingOutgoing,
            FriendshipStatus.Pending => StatusPendingIncoming,
            _ => StatusNone
        };
    }
}
