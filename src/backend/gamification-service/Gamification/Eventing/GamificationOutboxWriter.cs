using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Eventing;

/// <summary>
/// Serializes an event envelope into an outbox row on the caller's <c>DbContext</c>.
///
/// <para>
/// Adds to the change tracker and nothing more — it never calls <c>SaveChangesAsync</c>, which is the
/// entire point: the row commits with the business data that produced it or not at all.
/// </para>
///
/// <para>
/// The organization is copied from the ambient <c>ITenantContext</c> onto the row, so the system-mode
/// relay can republish the event under the tenant it belongs to without having to open the payload.
/// </para>
/// </summary>
internal sealed class GamificationOutboxWriter : IOutboxWriter
{
    private readonly GamificationDbContext _databaseContext;
    private readonly ITenantContext _tenantContext;

    public GamificationOutboxWriter(GamificationDbContext databaseContext, ITenantContext tenantContext)
    {
        _databaseContext = databaseContext;
        _tenantContext = tenantContext;
    }

    public void Enqueue<TData>(string topic, string partitionKey, string eventType, TData data, int version = 1)
    {
        var envelope = EventEnvelope.Create(eventType, data, version, _tenantContext.OrganizationId);
        var payload = System.Text.Json.JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);

        _databaseContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = envelope.EventId,
            OrganizationId = envelope.OrganizationId,
            Topic = topic,
            PartitionKey = partitionKey,
            Payload = payload,
            OccurredAt = envelope.OccurredAt,
        });
    }
}
