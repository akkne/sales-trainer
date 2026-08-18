using System.Text.Json;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Eventing;

/// <summary>
/// Stages an outgoing event as a row on the caller's <see cref="LearningDbContext"/> change tracker,
/// never producing to a broker itself.
///
/// <para>
/// The envelope's organization is stamped from <see cref="ITenantContext"/> at enqueue time, not read
/// back at dispatch time: the relay runs outside any request and would have no tenant to stamp. A
/// caller running in system mode therefore stages a message with no organization, which is correct for
/// platform-global projections and wrong for tenant data — so tenant writes must always happen with an
/// organization in context.
/// </para>
///
/// <para>
/// Nothing here calls <c>SaveChanges</c>. The row commits with the caller's transaction, which is the
/// whole point of the outbox.
/// </para>
/// </summary>
internal sealed class LearningOutboxWriter : IOutboxWriter
{
    private readonly LearningDbContext _databaseContext;
    private readonly ITenantContext _tenantContext;

    public LearningOutboxWriter(LearningDbContext databaseContext, ITenantContext tenantContext)
    {
        _databaseContext = databaseContext;
        _tenantContext = tenantContext;
    }

    public void Enqueue<TData>(string topic, string partitionKey, string eventType, TData data, int version = 1)
    {
        var envelope = EventEnvelope.Create(eventType, data, version, _tenantContext.OrganizationId);
        var payload = JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);

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
