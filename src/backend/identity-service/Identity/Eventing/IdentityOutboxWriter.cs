using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Eventing;

/// <summary>
/// Stages an integration event as a row in identity-db's outbox, inside the caller's transaction, so
/// it cannot be published without the state change that justified it. The organization is stamped from
/// <see cref="ITenantContext"/> rather than taken from the caller — a platform-wide write leaves it
/// null, which is what consumers read as "not scoped to one tenant".
/// </summary>
internal sealed class IdentityOutboxWriter : IOutboxWriter
{
    private readonly IdentityDbContext _databaseContext;
    private readonly ITenantContext _tenantContext;

    public IdentityOutboxWriter(IdentityDbContext databaseContext, ITenantContext tenantContext)
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
