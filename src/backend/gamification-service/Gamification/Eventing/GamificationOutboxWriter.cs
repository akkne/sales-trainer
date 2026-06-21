using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Eventing;

internal sealed class GamificationOutboxWriter : IOutboxWriter
{
    private readonly GamificationDbContext _databaseContext;

    public GamificationOutboxWriter(GamificationDbContext databaseContext)
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
