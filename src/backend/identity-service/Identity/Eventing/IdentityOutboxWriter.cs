using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Eventing;

internal sealed class IdentityOutboxWriter : IOutboxWriter
{
    private readonly IdentityDbContext _databaseContext;

    public IdentityOutboxWriter(IdentityDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public void Enqueue<TData>(string topic, string partitionKey, string eventType, TData data, int version = 1)
    {
        var envelope = EventEnvelope.Create(eventType, data, version);
        var payload = System.Text.Json.JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);

        _databaseContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = envelope.EventId,
            Topic = topic,
            PartitionKey = partitionKey,
            Payload = payload,
            OccurredAt = envelope.OccurredAt,
        });
    }
}
