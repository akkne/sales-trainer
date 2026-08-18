using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Friends.Constants;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Features.Friends.Services.Abstract;
using Sellevate.Social.Identity;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Features.Friends.Services.Implementation;

/// <summary>
/// Phase 40.13. Friendships are tenant data: the row carries the organization, the query filter in
/// <see cref="SocialDbContext"/> hides other organizations' rows, and the row-level-security policy
/// enforces it even when the filter is bypassed. Two people in different organizations therefore
/// cannot become friends — the row one of them creates is invisible to the other, so it can never be
/// accepted — and because <c>ChatService</c> refuses to open a conversation without an accepted
/// friendship, a chat cannot cross the boundary either.
///
/// <para>
/// Every method opens a <see cref="TenantTransactionScope"/> as its first statement. Without one,
/// <c>SET LOCAL app.organization_id</c> has nothing to apply to and RLS returns zero rows — which on
/// this screen is indistinguishable from having no friends yet.
/// </para>
///
/// <para>
/// <c>UserReplicas</c> is deliberately not tenant-scoped (docs/TENANCY/TENANCY.md §4.2, and the same
/// call every other service made), so <see cref="SearchUsersAsync"/> still searches the platform
/// directory. See docs/DECISIONS.md, "social-service's user directory stays platform-global in
/// 40.13", for what that does and does not expose, and for the membership projection that closes it.
/// </para>
/// </summary>
internal sealed class FriendService(
    SocialDbContext databaseContext,
    ISocialEventPublisher eventPublisher) : IFriendService
{
    private const string UnknownDisplayName = "Пользователь";

    public async Task<List<FriendDto>> GetFriendsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

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
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

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
                    isIncoming ? FriendRequestDirections.Incoming : FriendRequestDirections.Outgoing,
                    friendship.CreatedAt);
            })
            .OrderByDescending(request => request.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Sends, or revives, a pending request between two people. A previously declined pair is reused
    /// rather than re-created, so declining does not block a later attempt, and the request is
    /// re-pointed at whoever is asking this time.
    ///
    /// <para>
    /// The insert is guarded because two people can ask each other at the same instant: the
    /// canonical-pair unique index rejects the reciprocal row, and that rejection means "a request
    /// already exists" rather than a failure worth surfacing.
    /// </para>
    /// </summary>
    public async Task<Friendship> SendFriendRequestAsync(
        Guid requesterId,
        Guid addresseeId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        if (requesterId == addresseeId)
            throw new InvalidOperationException(ErrorMessages.FriendRequestToSelf);

        var addresseeExists = await databaseContext.UserReplicas
            .AnyAsync(replica => replica.UserId == addresseeId, cancellationToken);

        if (!addresseeExists)
            throw new KeyNotFoundException(ErrorMessages.FriendRequestTargetNotFound);

        var existingFriendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                (friendship.RequesterId == requesterId && friendship.AddresseeId == addresseeId) ||
                (friendship.RequesterId == addresseeId && friendship.AddresseeId == requesterId),
                cancellationToken);

        if (existingFriendship is not null)
        {
            if (existingFriendship.Status == FriendshipStatus.Accepted)
                throw new InvalidOperationException(ErrorMessages.FriendRequestAlreadyFriends);

            if (existingFriendship.Status == FriendshipStatus.Pending)
                throw new InvalidOperationException(ErrorMessages.FriendRequestAlreadyExists);

            if (existingFriendship.Status == FriendshipStatus.Declined)
            {
                existingFriendship.RequesterId = requesterId;
                existingFriendship.AddresseeId = addresseeId;
                existingFriendship.Status = FriendshipStatus.Pending;
                existingFriendship.CreatedAt = DateTime.UtcNow;
                existingFriendship.AcceptedAt = null;
                await databaseContext.SaveChangesAsync(cancellationToken);
                await scope.CommitAsync(cancellationToken);

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
        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException(
                ErrorMessages.FriendRequestAlreadyExists, exception);
        }

        await scope.CommitAsync(cancellationToken);

        await PublishFriendRequestReceivedAsync(friendship, cancellationToken);

        return friendship;
    }

    public async Task AcceptFriendRequestAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship => friendship.Id == friendshipId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.FriendRequestNotFound);

        if (friendship.AddresseeId != userId)
            throw new InvalidOperationException(ErrorMessages.FriendRequestAcceptorMustBeAddressee);

        if (friendship.Status != FriendshipStatus.Pending)
            throw new InvalidOperationException(ErrorMessages.FriendRequestNotPending);

        friendship.Status = FriendshipStatus.Accepted;
        friendship.AcceptedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);

        await PublishFriendRequestAcceptedAsync(friendship, cancellationToken);
    }

    public async Task DeclineFriendRequestAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship => friendship.Id == friendshipId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.FriendRequestNotFound);

        if (friendship.AddresseeId != userId)
            throw new InvalidOperationException(ErrorMessages.FriendRequestDeclinerMustBeAddressee);

        if (friendship.Status != FriendshipStatus.Pending)
            throw new InvalidOperationException(ErrorMessages.FriendRequestNotPending);

        friendship.Status = FriendshipStatus.Declined;
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Withdraws a request the caller sent. The row is hard-deleted rather than left as a declined
    /// tombstone — withdrawing returns the pair to "no request ever happened", so either party may
    /// start fresh afterwards. Declining is the opposite and keeps the row, which is what lets the
    /// requester revive it later.
    /// </summary>
    public async Task CancelFriendRequestAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship => friendship.Id == friendshipId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.FriendRequestNotFound);

        if (friendship.RequesterId != userId)
            throw new InvalidOperationException(ErrorMessages.FriendRequestCancellerMustBeRequester);

        if (friendship.Status != FriendshipStatus.Pending)
            throw new InvalidOperationException(ErrorMessages.FriendRequestNotPending);

        databaseContext.Friendships.Remove(friendship);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public async Task RemoveFriendAsync(
        Guid userId,
        Guid friendUserId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var friendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                friendship.Status == FriendshipStatus.Accepted &&
                ((friendship.RequesterId == userId && friendship.AddresseeId == friendUserId) ||
                 (friendship.RequesterId == friendUserId && friendship.AddresseeId == userId)),
                cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.FriendshipNotFound);

        databaseContext.Friendships.Remove(friendship);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(
        Guid currentUserId,
        string query,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        if (string.IsNullOrWhiteSpace(query) || query.Length < FriendSearchConstants.MinimumQueryLength)
            return [];

        var escapedQuery = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var searchPattern = $"%{escapedQuery}%";

        var matchingReplicas = await databaseContext.UserReplicas
            .Where(replica =>
                replica.UserId != currentUserId &&
                (EF.Functions.ILike(replica.DisplayName, searchPattern, "\\") ||
                 EF.Functions.ILike(replica.Email, searchPattern, "\\")))
            .Take(FriendSearchConstants.MaximumResultCount)
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
                    AvatarUrls.For(replica.UserId),
                    friendship?.Id);
            })
            .ToList();
    }

    public async Task<PublicProfileDto> GetPublicProfileAsync(
        Guid viewerUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var targetReplica = await databaseContext.UserReplicas
            .FirstOrDefaultAsync(replica => replica.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.UserNotFound);

        var existingFriendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                (friendship.RequesterId == viewerUserId && friendship.AddresseeId == targetUserId) ||
                (friendship.RequesterId == targetUserId && friendship.AddresseeId == viewerUserId),
                cancellationToken);

        var friendshipStatus = viewerUserId == targetUserId
            ? FriendshipStatusValues.None
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
            AvatarUrls.For(targetReplica.UserId),
            viewerUserId == targetUserId ? null : existingFriendship?.Id);
    }

    public async Task<List<FriendLeaderboardEntryDto>> GetFriendLeaderboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

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

    public Task<List<FriendActivityDto>> GetFriendActivityFeedAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<FriendActivityDto>());

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
            return FriendshipStatusValues.None;

        return friendship.Status switch
        {
            FriendshipStatus.Accepted => FriendshipStatusValues.Friends,
            FriendshipStatus.Pending when friendship.RequesterId == currentUserId => FriendshipStatusValues.PendingOutgoing,
            FriendshipStatus.Pending => FriendshipStatusValues.PendingIncoming,
            _ => FriendshipStatusValues.None
        };
    }

    /// <summary>
    /// Distinguishes "somebody got there first" from every other write failure, so the caller can
    /// answer with the same message it would have given had it seen the existing row.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
