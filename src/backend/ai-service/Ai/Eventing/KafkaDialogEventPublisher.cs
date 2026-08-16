using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Eventing;

/// <summary>
/// Publishes <c>dialog.evaluated</c> — the event that turns a finished roleplay into XP.
///
/// <para>
/// <b>40.14.</b> This was the one publisher in the codebase still leaving
/// <c>organizationId</c> at its <see langword="null"/> default, and the omission was not
/// cosmetic. gamification-service's <c>DialogEvaluatedConsumer</c> leaves
/// <c>RequiresOrganization</c> at its inherited <c>true</c> — correctly, since it writes into
/// tenant-scoped XP tables — so an envelope with no tenant is rejected, retried and dead-lettered.
/// Every completed roleplay's XP grant was being lost, quietly, in the dead-letter topic.
/// </para>
///
/// <para>
/// The tempting fix is the wrong one: flipping the consumer to <c>RequiresOrganization = false</c>
/// clears the error by putting the handler in system mode, where the write guard steps aside and
/// the XP rows land with <c>OrganizationId = Guid.Empty</c> — invisible to every organization and
/// permanently unattributable. The tenant has to be supplied at the producer, which is the only
/// place that still knows it.
/// </para>
///
/// <para>
/// It comes from <see cref="ITenantContext"/> rather than from the caller for the same reason as
/// <c>KafkaSocialEventPublisher</c>: this event is always published from inside an HTTP request,
/// where the ambient tenant is the request's tenant by construction. company-service's follow-up
/// reminder passes it explicitly instead, because it runs in a background job where the tenant is a
/// property of the unit of work rather than of the caller.
/// </para>
/// </summary>
internal sealed class KafkaDialogEventPublisher(
    IEventPublisher eventPublisher,
    ITenantContext tenantContext) : IDialogEventPublisher
{
    public Task PublishEvaluatedAsync(DialogEvaluatedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.DialogEvaluated, payload.UserId.ToString(), Topics.DialogEvaluated, payload,
            organizationId: RequireOrganizationId(), cancellationToken: cancellationToken);

    /// <summary>
    /// Fails closed and loudly, with the message every other tenancy guard in the codebase uses so
    /// operators grep once. Surfacing here is the whole point: an event published with no tenant is
    /// a bug that must break the request that caused it, not go quiet three services downstream.
    /// </summary>
    private Guid RequireOrganizationId()
        => tenantContext.OrganizationId
           ?? throw new InvalidOperationException("Organization context is not set.");
}
