using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Eventing;

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
