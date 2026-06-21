using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Eventing;

internal sealed class LearningOutboxWriter : IOutboxWriter
{
    private readonly LearningDbContext _databaseContext;

    public LearningOutboxWriter(LearningDbContext databaseContext)
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
