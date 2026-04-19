using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Friends.Models;
using SalesTrainer.Api.Features.Friends.Services.Abstract;
using SalesTrainer.Api.Features.Notifications.Models;
using SalesTrainer.Api.Features.Notifications.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Friends.Services.Implementation;

internal sealed class FriendService(
    AppDbContext databaseContext,
    INotificationService notificationService) : IFriendService
{
    private const int MaximumSearchResultCount = 20;
    private const string DirectionIncoming = "incoming";
    private const string DirectionOutgoing = "outgoing";
    private const string StatusNone = "none";
    private const string StatusPendingOutgoing = "pending_outgoing";
    private const string StatusPendingIncoming = "pending_incoming";
    private const string StatusFriends = "friends";

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

        var friendUsers = await databaseContext.Users
            .Where(user => friendUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        var friendProfiles = await databaseContext.UserProfiles
            .Where(profile => friendUserIds.Contains(profile.UserId))
            .ToListAsync(cancellationToken);

        var friendXpTotals = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => friendUserIds.Contains(experiencePointRecord.UserId))
            .GroupBy(experiencePointRecord => experiencePointRecord.UserId)
            .Select(group => new { UserId = group.Key, TotalAmount = group.Sum(record => record.Amount) })
            .ToListAsync(cancellationToken);

        var friendStreaks = await databaseContext.UserStreaks
            .Where(streak => friendUserIds.Contains(streak.UserId))
            .ToListAsync(cancellationToken);

        var friendAchievementCounts = await databaseContext.UserAchievements
            .Where(achievement => friendUserIds.Contains(achievement.UserId))
            .GroupBy(achievement => achievement.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return friendUsers
            .Select(user =>
            {
                var profile = friendProfiles.FirstOrDefault(profile => profile.UserId == user.Id);
                var xpTotal = friendXpTotals.FirstOrDefault(xp => xp.UserId == user.Id);
                var streak = friendStreaks.FirstOrDefault(streak => streak.UserId == user.Id);
                var achievementCount = friendAchievementCounts.FirstOrDefault(achievement => achievement.UserId == user.Id);

                return new FriendDto(
                    user.Id,
                    user.DisplayName,
                    profile?.Persona,
                    xpTotal?.TotalAmount ?? 0,
                    streak?.CurrentStreakDayCount ?? 0,
                    achievementCount?.Count ?? 0);
            })
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
            .Where(identifer => identifer != userId)
            .Distinct()
            .ToList();

        var involvedUsers = await databaseContext.Users
            .Where(user => involvedUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        var involvedProfiles = await databaseContext.UserProfiles
            .Where(profile => involvedUserIds.Contains(profile.UserId))
            .ToListAsync(cancellationToken);

        return pendingFriendships
            .Select(friendship =>
            {
                var isIncoming = friendship.AddresseeId == userId;
                var otherUserId = isIncoming ? friendship.RequesterId : friendship.AddresseeId;
                var otherUser = involvedUsers.First(user => user.Id == otherUserId);
                var otherProfile = involvedProfiles.FirstOrDefault(profile => profile.UserId == otherUserId);

                return new FriendRequestDto(
                    friendship.Id,
                    otherUserId,
                    otherUser.DisplayName,
                    otherProfile?.Persona,
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

        var addresseeExists = await databaseContext.Users
            .AnyAsync(user => user.Id == addresseeId, cancellationToken);

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

                await CreateFriendRequestReceivedNotificationAsync(existingFriendship, cancellationToken);

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

        await CreateFriendRequestReceivedNotificationAsync(friendship, cancellationToken);

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

        await CreateFriendRequestAcceptedNotificationAsync(friendship, cancellationToken);
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

        var matchingUsers = await databaseContext.Users
            .Where(user =>
                user.Id != currentUserId &&
                (EF.Functions.ILike(user.DisplayName, searchPattern) ||
                 EF.Functions.ILike(user.Email, searchPattern)))
            .Take(MaximumSearchResultCount)
            .ToListAsync(cancellationToken);

        if (matchingUsers.Count == 0)
            return [];

        var matchingUserIds = matchingUsers.Select(user => user.Id).ToList();

        var existingFriendships = await databaseContext.Friendships
            .Where(friendship =>
                (friendship.RequesterId == currentUserId && matchingUserIds.Contains(friendship.AddresseeId)) ||
                (matchingUserIds.Contains(friendship.RequesterId) && friendship.AddresseeId == currentUserId))
            .ToListAsync(cancellationToken);

        var matchingProfiles = await databaseContext.UserProfiles
            .Where(profile => matchingUserIds.Contains(profile.UserId))
            .ToListAsync(cancellationToken);

        return matchingUsers
            .Select(user =>
            {
                var friendship = existingFriendships.FirstOrDefault(friendship =>
                    (friendship.RequesterId == currentUserId && friendship.AddresseeId == user.Id) ||
                    (friendship.RequesterId == user.Id && friendship.AddresseeId == currentUserId));

                var friendshipStatus = ResolveFriendshipStatus(friendship, currentUserId);
                var profile = matchingProfiles.FirstOrDefault(profile => profile.UserId == user.Id);

                return new UserSearchResultDto(
                    user.Id,
                    user.DisplayName,
                    profile?.Persona,
                    friendshipStatus);
            })
            .ToList();
    }

    public async Task<PublicProfileDto> GetPublicProfileAsync(
        Guid viewerUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var targetUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.Id == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        var targetProfile = await databaseContext.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == targetUserId, cancellationToken);

        var totalExperiencePointsAmount = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => experiencePointRecord.UserId == targetUserId)
            .SumAsync(experiencePointRecord => (int?)experiencePointRecord.Amount, cancellationToken) ?? 0;

        var streakRecord = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == targetUserId, cancellationToken);

        var achievementCount = await databaseContext.UserAchievements
            .CountAsync(achievement => achievement.UserId == targetUserId, cancellationToken);

        var averageExerciseScore = await databaseContext.UserExerciseAttempts
            .Where(attempt => attempt.UserId == targetUserId)
            .AverageAsync(attempt => (double?)attempt.Score, cancellationToken) ?? 0.0;

        var existingFriendship = await databaseContext.Friendships
            .FirstOrDefaultAsync(friendship =>
                (friendship.RequesterId == viewerUserId && friendship.AddresseeId == targetUserId) ||
                (friendship.RequesterId == targetUserId && friendship.AddresseeId == viewerUserId),
                cancellationToken);

        var friendshipStatus = viewerUserId == targetUserId
            ? StatusNone
            : ResolveFriendshipStatus(existingFriendship, viewerUserId);

        return new PublicProfileDto(
            targetUser.Id,
            targetUser.DisplayName,
            targetProfile?.Persona,
            totalExperiencePointsAmount,
            streakRecord?.CurrentStreakDayCount ?? 0,
            achievementCount,
            Math.Round(averageExerciseScore, 1),
            friendshipStatus);
    }

    public async Task<List<FriendLeaderboardEntryDto>> GetFriendLeaderboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var friendUserIds = await GetAcceptedFriendUserIdsAsync(userId, cancellationToken);
        var allParticipantIds = friendUserIds.Append(userId).Distinct().ToList();

        var participantExperiencePoints = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => allParticipantIds.Contains(experiencePointRecord.UserId))
            .GroupBy(experiencePointRecord => experiencePointRecord.UserId)
            .Select(group => new { UserId = group.Key, TotalAmount = group.Sum(record => record.Amount) })
            .OrderByDescending(entry => entry.TotalAmount)
            .ToListAsync(cancellationToken);

        var participantUsers = await databaseContext.Users
            .Where(user => allParticipantIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        var rankedEntries = participantExperiencePoints
            .Select((entry, index) =>
            {
                var user = participantUsers.First(user => user.Id == entry.UserId);
                return new FriendLeaderboardEntryDto(
                    entry.UserId,
                    user.DisplayName,
                    entry.TotalAmount,
                    index + 1,
                    entry.UserId == userId);
            })
            .ToList();

        var currentUserIncluded = rankedEntries.Any(entry => entry.UserId == userId);
        if (!currentUserIncluded)
        {
            var currentUser = participantUsers.FirstOrDefault(user => user.Id == userId);
            if (currentUser is not null)
            {
                rankedEntries.Add(new FriendLeaderboardEntryDto(
                    userId,
                    currentUser.DisplayName,
                    0,
                    rankedEntries.Count + 1,
                    true));
            }
        }

        return rankedEntries;
    }

    public async Task<List<FriendActivityDto>> GetFriendActivityFeedAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var friendUserIds = await GetAcceptedFriendUserIdsAsync(userId, cancellationToken);

        if (friendUserIds.Count == 0)
            return [];

        var friendUsers = await databaseContext.Users
            .Where(user => friendUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        var recentAchievements = await databaseContext.UserAchievements
            .Where(userAchievement => friendUserIds.Contains(userAchievement.UserId))
            .OrderByDescending(userAchievement => userAchievement.UnlockedAt)
            .Take(limit)
            .Join(
                databaseContext.Achievements,
                userAchievement => userAchievement.AchievementId,
                achievement => achievement.Id,
                (userAchievement, achievement) => new
                {
                    userAchievement.UserId,
                    userAchievement.UnlockedAt,
                    achievement.Title
                })
            .ToListAsync(cancellationToken);

        var recentExperiencePoints = await databaseContext.UserXpRecords
            .Where(experiencePointRecord =>
                friendUserIds.Contains(experiencePointRecord.UserId) &&
                experiencePointRecord.Source == "exercise")
            .OrderByDescending(experiencePointRecord => experiencePointRecord.EarnedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var activityItems = new List<FriendActivityDto>();

        foreach (var achievement in recentAchievements)
        {
            if (friendUsers.TryGetValue(achievement.UserId, out var displayName))
            {
                activityItems.Add(new FriendActivityDto(
                    achievement.UserId,
                    displayName,
                    "earned_achievement",
                    achievement.Title,
                    achievement.UnlockedAt));
            }
        }

        foreach (var experiencePointRecord in recentExperiencePoints)
        {
            if (friendUsers.TryGetValue(experiencePointRecord.UserId, out var displayName))
            {
                activityItems.Add(new FriendActivityDto(
                    experiencePointRecord.UserId,
                    displayName,
                    "earned_xp",
                    $"+{experiencePointRecord.Amount} XP",
                    experiencePointRecord.EarnedAt));
            }
        }

        return activityItems
            .OrderByDescending(activity => activity.OccurredAt)
            .Take(limit)
            .ToList();
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

    private async Task CreateFriendRequestReceivedNotificationAsync(
        Friendship friendship,
        CancellationToken cancellationToken)
    {
        var requesterDisplayName = await databaseContext.Users
            .Where(user => user.Id == friendship.RequesterId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Пользователь";

        await notificationService.CreateAsync(
            recipientUserId: friendship.AddresseeId,
            notificationType: NotificationType.FriendRequestReceived,
            title: "Новая заявка в друзья",
            body: $"{requesterDisplayName} хочет добавить вас в друзья.",
            actionUrl: "/friends?tab=requests",
            relatedEntityId: friendship.Id.ToString(),
            cancellationToken: cancellationToken);
    }

    private async Task CreateFriendRequestAcceptedNotificationAsync(
        Friendship friendship,
        CancellationToken cancellationToken)
    {
        var addresseeDisplayName = await databaseContext.Users
            .Where(user => user.Id == friendship.AddresseeId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Пользователь";

        await notificationService.CreateAsync(
            recipientUserId: friendship.RequesterId,
            notificationType: NotificationType.FriendRequestAccepted,
            title: "Заявка принята",
            body: $"{addresseeDisplayName} принял(а) вашу заявку в друзья.",
            actionUrl: $"/friends/{friendship.AddresseeId}",
            relatedEntityId: friendship.Id.ToString(),
            cancellationToken: cancellationToken);
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
