using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.Notification.Features.Notifications.Emails;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Implementation;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Tests.Unit;

[TestFixture]
public class NotificationServiceTests
{
    private static readonly Guid RecipientUserId = Guid.NewGuid();

    /// <summary>Captures dispatch calls so tests can assert whether an email was triggered.</summary>
    private sealed class RecordingEmailDispatcher : INotificationEmailDispatcher
    {
        public List<(Guid Recipient, NotificationType Type, string Title, string Body, string? ActionUrl)> Dispatched { get; } = [];

        public Task DispatchAsync(
            Guid recipientUserId,
            NotificationType notificationType,
            string title,
            string body,
            string? actionUrl,
            CancellationToken cancellationToken = default)
        {
            Dispatched.Add((recipientUserId, notificationType, title, body, actionUrl));
            return Task.CompletedTask;
        }
    }

    private static NotificationService CreateService(
        InMemoryNotificationStore store,
        int inboxCapacity = 100,
        int retentionDays = 30,
        INotificationEmailDispatcher? emailDispatcher = null)
    {
        var configuration = Options.Create(new NotificationStorageConfiguration
        {
            InboxCapacity = inboxCapacity,
            RetentionDays = retentionDays
        });

        return new NotificationService(
            store,
            emailDispatcher ?? new RecordingEmailDispatcher(),
            configuration,
            NullLogger<NotificationService>.Instance);
    }

    private static CreateNotificationRequest BuildRequest(
        NotificationType type = NotificationType.AchievementUnlocked,
        string relatedEntityId = "related-1") =>
        new(RecipientUserId, type, "Title", "Body", "/profile", relatedEntityId);

    [Test]
    public async Task CreateAsync_WritesNotificationIntoRecipientInbox()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);

        await service.CreateAsync(BuildRequest());

        var stored = await store.GetAllAsync(RecipientUserId);
        stored.Should().ContainSingle();
        stored[0].Title.Should().Be("Title");
        stored[0].IsRead.Should().BeFalse();
    }

    [Test]
    public async Task CreateAsync_AppliesConfiguredRetentionAsTtl()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store, retentionDays: 30);

        await service.CreateAsync(BuildRequest());

        store.LastRetention.Should().Be(TimeSpan.FromDays(30));
    }

    [Test]
    public async Task CreateAsync_WithBlankTitle_Throws()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        var request = new CreateNotificationRequest(
            RecipientUserId, NotificationType.StreakMilestone, "   ", "Body", null, null);

        var act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task CreateAsync_CapsInboxAtConfiguredCapacity()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store, inboxCapacity: 3);

        for (var created = 0; created < 10; created++)
        {
            // Distinct relatedEntityId per notification — these are genuinely different
            // notifications, not domain-event replays, so they are not deduplicated.
            await service.CreateAsync(BuildRequest(relatedEntityId: $"related-{created}"));
        }

        store.CapacityFor(RecipientUserId).Should().Be(3);
    }

    [Test]
    public async Task GetUnreadCount_CountsOnlyUnread()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest(relatedEntityId: "related-1"));
        await service.CreateAsync(BuildRequest(relatedEntityId: "related-2"));

        var unreadCount = await service.GetUnreadCountAsync(RecipientUserId);

        unreadCount.Should().Be(2);
    }

    [Test]
    public async Task GetRecentAsync_NewestFirst_AndHonorsLimit()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest(NotificationType.FriendRequestReceived));
        await Task.Delay(2);
        await service.CreateAsync(BuildRequest(NotificationType.StreakMilestone));

        var recent = await service.GetRecentAsync(RecipientUserId, limit: 1, includeRead: true);

        recent.Should().ContainSingle();
        recent[0].NotificationType.Should().Be(nameof(NotificationType.StreakMilestone));
    }

    [Test]
    public async Task GetRecentAsync_WithIncludeReadFalse_ExcludesReadNotifications()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest());
        var stored = await store.GetAllAsync(RecipientUserId);
        await service.MarkAsReadAsync(RecipientUserId, stored[0].Id);

        var unreadOnly = await service.GetRecentAsync(RecipientUserId, limit: 20, includeRead: false);

        unreadOnly.Should().BeEmpty();
    }

    [Test]
    public async Task MarkAsReadAsync_MarksTheTargetAndDropsUnreadCount()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest());
        var stored = await store.GetAllAsync(RecipientUserId);

        await service.MarkAsReadAsync(RecipientUserId, stored[0].Id);

        var refreshed = await store.GetAllAsync(RecipientUserId);
        refreshed[0].IsRead.Should().BeTrue();
        refreshed[0].ReadAt.Should().NotBeNull();
        (await service.GetUnreadCountAsync(RecipientUserId)).Should().Be(0);
    }

    [Test]
    public async Task MarkAsReadAsync_IsIdempotentForAlreadyReadNotification()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest());
        var stored = await store.GetAllAsync(RecipientUserId);
        await service.MarkAsReadAsync(RecipientUserId, stored[0].Id);
        var firstReadAt = (await store.GetAllAsync(RecipientUserId))[0].ReadAt;

        await service.MarkAsReadAsync(RecipientUserId, stored[0].Id);

        (await store.GetAllAsync(RecipientUserId))[0].ReadAt.Should().Be(firstReadAt);
    }

    [Test]
    public async Task MarkAsReadAsync_UnknownNotification_Throws()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest());

        var act = async () => await service.MarkAsReadAsync(RecipientUserId, Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task MarkAllAsReadAsync_MarksEverythingRead()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        await service.CreateAsync(BuildRequest());
        await service.CreateAsync(BuildRequest());

        await service.MarkAllAsReadAsync(RecipientUserId);

        (await service.GetUnreadCountAsync(RecipientUserId)).Should().Be(0);
        (await store.GetAllAsync(RecipientUserId)).Should().OnlyContain(notification => notification.IsRead);
    }

    // ── NO3: domain-level idempotency ─────────────────────────────────────────

    [Test]
    public async Task CreateAsync_SameDomainEvent_DoesNotCreateDuplicateNotification()
    {
        // Two calls with identical RecipientUserId + NotificationType + RelatedEntityId
        // simulate the same domain event being replayed (e.g. Kafka redelivery).
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        var request = BuildRequest(); // type=AchievementUnlocked, relatedEntityId="related-1"

        await service.CreateAsync(request);
        await service.CreateAsync(request); // replay — must be ignored

        var stored = await store.GetAllAsync(RecipientUserId);
        stored.Should().ContainSingle("second create with the same business key must be skipped");
    }

    [Test]
    public async Task CreateAsync_DifferentRelatedEntityId_CreatesBothNotifications()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);

        var first  = new CreateNotificationRequest(RecipientUserId, NotificationType.AchievementUnlocked, "T", "B", "/profile", "entity-1");
        var second = new CreateNotificationRequest(RecipientUserId, NotificationType.AchievementUnlocked, "T", "B", "/profile", "entity-2");

        await service.CreateAsync(first);
        await service.CreateAsync(second);

        (await store.GetAllAsync(RecipientUserId)).Should().HaveCount(2);
    }

    [Test]
    public async Task CreateAsync_DifferentNotificationType_CreatesBothNotifications()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);

        var first  = new CreateNotificationRequest(RecipientUserId, NotificationType.AchievementUnlocked, "T", "B", "/profile", "same-entity");
        var second = new CreateNotificationRequest(RecipientUserId, NotificationType.StreakMilestone,     "T", "B", "/profile", "same-entity");

        await service.CreateAsync(first);
        await service.CreateAsync(second);

        (await store.GetAllAsync(RecipientUserId)).Should().HaveCount(2);
    }

    // ── Email side channel ────────────────────────────────────────────────────

    [Test]
    public async Task CreateAsync_WithSendEmailTrue_DispatchesEmail()
    {
        var store = new InMemoryNotificationStore();
        var dispatcher = new RecordingEmailDispatcher();
        var service = CreateService(store, emailDispatcher: dispatcher);
        var request = new CreateNotificationRequest(
            RecipientUserId, NotificationType.LeagueUpdated, "League", "You were promoted",
            "/league", "league-1", SendEmail: true);

        await service.CreateAsync(request);

        dispatcher.Dispatched.Should().ContainSingle();
        dispatcher.Dispatched[0].Type.Should().Be(NotificationType.LeagueUpdated);
        dispatcher.Dispatched[0].Recipient.Should().Be(RecipientUserId);
    }

    [Test]
    public async Task CreateAsync_WithSendEmailFalse_DoesNotDispatchEmail()
    {
        var store = new InMemoryNotificationStore();
        var dispatcher = new RecordingEmailDispatcher();
        var service = CreateService(store, emailDispatcher: dispatcher);

        await service.CreateAsync(BuildRequest()); // SendEmail defaults to false

        dispatcher.Dispatched.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAsync_DeduplicatedReplay_DoesNotDispatchEmailTwice()
    {
        var store = new InMemoryNotificationStore();
        var dispatcher = new RecordingEmailDispatcher();
        var service = CreateService(store, emailDispatcher: dispatcher);
        var request = new CreateNotificationRequest(
            RecipientUserId, NotificationType.DiscussReplyReceived, "Reply", "Sam replied",
            "/discuss/1", "reply-1", SendEmail: true);

        await service.CreateAsync(request);
        await service.CreateAsync(request); // replay — deduped, must not email again

        dispatcher.Dispatched.Should().ContainSingle();
    }

    // ── NO2: input sanitization ───────────────────────────────────────────────

    [Test]
    public async Task CreateAsync_ControlCharactersInTitleAndBody_AreStripped()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        var request = new CreateNotificationRequest(
            RecipientUserId,
            NotificationType.StreakMilestone,
            "Title withcontrols",
            "Body\twith\rnewlines\n",
            "/profile",
            "entity-ctrl");

        await service.CreateAsync(request);

        var stored = await store.GetAllAsync(RecipientUserId);
        stored.Should().ContainSingle();
        stored[0].Title.Should().NotContainAny(" ", "");
        stored[0].Body.Should().NotContainAny("\t", "\r", "\n");
    }

    [Test]
    public async Task CreateAsync_RelativeActionUrl_IsPreserved()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        var request = new CreateNotificationRequest(
            RecipientUserId, NotificationType.AchievementUnlocked, "T", "B", "/profile", "e1");

        await service.CreateAsync(request);

        (await store.GetAllAsync(RecipientUserId))[0].ActionUrl.Should().Be("/profile");
    }

    [Test]
    public async Task CreateAsync_AbsoluteActionUrl_IsRejectedAndStoredAsNull()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        var request = new CreateNotificationRequest(
            RecipientUserId, NotificationType.AchievementUnlocked, "T", "B",
            "https://evil.example.com/steal", "e-abs");

        await service.CreateAsync(request);

        (await store.GetAllAsync(RecipientUserId))[0].ActionUrl.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_JavascriptSchemeActionUrl_IsRejectedAndStoredAsNull()
    {
        var store = new InMemoryNotificationStore();
        var service = CreateService(store);
        var request = new CreateNotificationRequest(
            RecipientUserId, NotificationType.AchievementUnlocked, "T", "B",
            "javascript:alert(1)", "e-js");

        await service.CreateAsync(request);

        (await store.GetAllAsync(RecipientUserId))[0].ActionUrl.Should().BeNull();
    }
}
