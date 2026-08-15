using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Emails;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Implementation;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Tests.Unit;

/// <summary>
/// Phase 40.13 tripwires for notification-service. Fast, infrastructure-free, run on every build.
///
/// <para>
/// notification-service has no database and therefore no row-level security to fall back on: the
/// Redis key <em>is</em> the boundary. These tests assert the key shape and the failure mode
/// directly, because there is no second mechanism that would catch a mistake here.
/// </para>
/// </summary>
[TestFixture]
public class NotificationTenancyTests
{
    private static readonly Guid OrganizationA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RecipientUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Test]
    public void Inbox_keys_carry_the_organization_prefix()
    {
        RedisKeys.Inbox(OrganizationA, RecipientUserId)
            .Should().StartWith($"org:{OrganizationA:N}:")
            .And.Be($"org:{OrganizationA:N}:notifications:inbox:{RecipientUserId:N}");
    }

    [Test]
    public void Unread_count_keys_carry_the_organization_prefix()
    {
        RedisKeys.UnreadCount(OrganizationA, RecipientUserId)
            .Should().StartWith($"org:{OrganizationA:N}:");
    }

    [Test]
    public void Chat_email_watermark_keys_carry_the_organization_prefix()
    {
        RedisKeys.ChatEmailReadWatermark(OrganizationA, RecipientUserId, conversationId: null)
            .Should().StartWith($"org:{OrganizationA:N}:");
    }

    /// <summary>
    /// The same user in two organizations must not share an inbox. A user id is globally unique so
    /// this could not collide by accident — the assertion is about the key shape staying the thing
    /// that separates tenants, so a later refactor that drops the prefix fails here rather than in
    /// production.
    /// </summary>
    [Test]
    public void Two_organizations_never_share_an_inbox_key()
    {
        RedisKeys.Inbox(OrganizationA, RecipientUserId)
            .Should().NotBe(RedisKeys.Inbox(OrganizationB, RecipientUserId));
    }

    /// <summary>
    /// An unset tenant must raise, never build <c>org:00000000-...</c>. That key would be a single
    /// shared bucket collecting every caller whose context was missing — strictly worse than the
    /// un-prefixed key it replaced, because it would look correctly namespaced.
    /// </summary>
    [Test]
    public void An_unset_organization_raises_rather_than_building_a_zero_key()
    {
        var act = () => RedisKeys.Inbox(Guid.Empty, RecipientUserId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Organization context is not set.");
    }

    [Test]
    public async Task Creating_a_notification_without_a_tenant_raises()
    {
        var service = CreateServiceWithoutTenant();

        var act = () => service.CreateAsync(
            new CreateNotificationRequest(
                RecipientUserId, NotificationType.AchievementUnlocked, "Title", "Body", "/profile", "related-1"));

        (await act.Should().ThrowAsync<InvalidOperationException>(
                "an inbox has no platform-global reading — system mode here would mean every organization"))
            .WithMessage("Organization context is not set.");
    }

    [Test]
    public async Task Reading_notifications_without_a_tenant_raises()
    {
        var service = CreateServiceWithoutTenant();

        var act = () => service.GetRecentAsync(RecipientUserId, limit: 20, includeRead: true);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Organization context is not set.");
    }

    /// <summary>
    /// The end-to-end shape of the leak this block closes: a notification written for a user in one
    /// organization is invisible when the same user is read under another. The fake store is keyed
    /// by (organization, recipient) exactly like the real Redis key, so this fails if the
    /// organization ever stops reaching the store.
    /// </summary>
    [Test]
    public async Task A_notification_written_in_one_organization_is_invisible_in_another()
    {
        var store = new InMemoryNotificationStore();

        var serviceForA = CreateService(store, OrganizationA);
        await serviceForA.CreateAsync(new CreateNotificationRequest(
            RecipientUserId, NotificationType.AchievementUnlocked, "A's title", "Body", "/profile", "related-1"));

        var serviceForB = CreateService(store, OrganizationB);
        var seenByB = await serviceForB.GetRecentAsync(RecipientUserId, limit: 20, includeRead: true);

        seenByB.Should().BeEmpty();

        // Proven to exist, so the assertion above cannot pass on an empty store.
        var seenByA = await serviceForA.GetRecentAsync(RecipientUserId, limit: 20, includeRead: true);
        seenByA.Should().ContainSingle().Which.Title.Should().Be("A's title");
    }

    /// <summary>
    /// The unread badge is the most visible surface of the inbox, so it gets its own assertion
    /// rather than relying on the list read above.
    /// </summary>
    [Test]
    public async Task The_unread_count_does_not_include_another_organizations_notifications()
    {
        var store = new InMemoryNotificationStore();

        var serviceForA = CreateService(store, OrganizationA);
        await serviceForA.CreateAsync(new CreateNotificationRequest(
            RecipientUserId, NotificationType.AchievementUnlocked, "A's title", "Body", "/profile", "related-1"));

        var countForB = await CreateService(store, OrganizationB).GetUnreadCountAsync(RecipientUserId);
        var countForA = await serviceForA.GetUnreadCountAsync(RecipientUserId);

        countForB.Should().Be(0);
        countForA.Should().Be(1);
    }

    private static NotificationService CreateService(InMemoryNotificationStore store, Guid organizationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);
        return Build(store, tenantContext);
    }

    private static NotificationService CreateServiceWithoutTenant() =>
        Build(new InMemoryNotificationStore(), new TenantContext());

    private static NotificationService Build(InMemoryNotificationStore store, ITenantContext tenantContext) =>
        new(
            store,
            new NoOpEmailDispatcher(),
            tenantContext,
            Options.Create(new NotificationStorageConfiguration { InboxCapacity = 100, RetentionDays = 30 }),
            NullLogger<NotificationService>.Instance);

    private sealed class NoOpEmailDispatcher : INotificationEmailDispatcher
    {
        public Task DispatchAsync(
            Guid recipientUserId,
            NotificationType notificationType,
            string title,
            string body,
            string? actionUrl,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
