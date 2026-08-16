using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Eventing;

/// <summary>
/// Phase 40.13. Every event social-service publishes now carries the organization in its envelope.
///
/// <para>
/// This is not optional politeness: notification-service consumes all five of these topics and left
/// <c>RequiresOrganization</c> at its inherited <c>true</c> in this same block, because a
/// notification with no organization has no inbox to land in. An envelope without the tenant is
/// therefore a message that retries and dead-letters — a friend request that never notifies anyone.
/// </para>
///
/// <para>
/// The organization comes from <see cref="ITenantContext"/> rather than being passed in by each
/// caller because every one of these events is published from inside an HTTP request, where the
/// ambient tenant is the request's tenant by construction. That is the opposite of company-service's
/// follow-up reminder (40.12), which passes it explicitly — it runs in a background job, where the
/// tenant is a property of the unit of work rather than of the caller. <see cref="RequireOrganizationId"/>
/// throws for the same reason the Mongo repository does: an event published with no tenant is a bug
/// that must surface here, not three services downstream.
/// </para>
/// </summary>
internal sealed class KafkaSocialEventPublisher(
    IEventPublisher eventPublisher,
    ITenantContext tenantContext) : ISocialEventPublisher
{
    public Task PublishFriendRequestReceivedAsync(FriendRequestReceivedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.FriendRequestReceived, payload.RecipientId.ToString(), Topics.FriendRequestReceived, payload,
            organizationId: RequireOrganizationId(), cancellationToken: cancellationToken);

    public Task PublishFriendRequestAcceptedAsync(FriendRequestAcceptedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.FriendRequestAccepted, payload.RecipientId.ToString(), Topics.FriendRequestAccepted, payload,
            organizationId: RequireOrganizationId(), cancellationToken: cancellationToken);

    public Task PublishChatMessageSentAsync(ChatMessageSentEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.ChatMessageSent, payload.RecipientId.ToString(), Topics.ChatMessageSent, payload,
            organizationId: RequireOrganizationId(), cancellationToken: cancellationToken);

    public Task PublishChatMessageReadAsync(ChatMessageReadEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.ChatMessageRead, payload.ReaderUserId.ToString(), Topics.ChatMessageRead, payload,
            organizationId: RequireOrganizationId(), cancellationToken: cancellationToken);

    public Task PublishDiscussReplyCreatedAsync(DiscussReplyCreatedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.DiscussReplyCreated, payload.RecipientId.ToString(), Topics.DiscussReplyCreated, payload,
            organizationId: RequireOrganizationId(), cancellationToken: cancellationToken);

    /// <summary>
    /// Fails closed and loudly, with the message every other tenancy guard in the codebase uses so
    /// operators grep once.
    /// </summary>
    private Guid RequireOrganizationId()
        => tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");
}
