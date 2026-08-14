using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sellevate.BuildingBlocks.Eventing;

/// <summary>
/// The standard wrapper around every integration event placed on Kafka.
/// The shape is frozen across all services so producers and consumers — written
/// independently, deployed independently — always agree on the outer structure:
/// <c>{ eventId, occurredAt, type, version, organizationId, data }</c>.
///
/// <para>
/// <see cref="Data"/> is kept as a raw <see cref="JsonElement"/> so a consumer can
/// inspect <see cref="Type"/>/<see cref="Version"/> first and only then deserialize
/// the payload into the concrete contract it expects. Use
/// <see cref="EventEnvelope.Create{TData}"/> to build one and <see cref="DataAs{TData}"/>
/// to read the payload back.
/// </para>
///
/// <para>
/// <see cref="OrganizationId"/> is nullable on the wire: no producer populates it yet
/// (organization membership does not exist end to end until later Phase 40 blocks), and it
/// stays <c>null</c> for events that are genuinely platform-global rather than owned by one
/// tenant (e.g. Identity's cross-org user lifecycle events). A tenant-scoped consumer must
/// still treat a missing value as a hard failure rather than process the message with no
/// tenant context — see <c>Sellevate.BuildingBlocks.Messaging.KafkaConsumerBackgroundService</c>.
/// </para>
/// </summary>
public sealed record EventEnvelope
{
    /// <summary>Unique id of this event instance. Consumers dedupe on this (at-least-once delivery).</summary>
    public required Guid EventId { get; init; }

    /// <summary>When the event occurred, in UTC.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Logical event type, e.g. <c>user.registered</c> (matches the topic name by convention).</summary>
    public required string Type { get; init; }

    /// <summary>Schema version of <see cref="Data"/>; lets payloads evolve without breaking old consumers.</summary>
    public required int Version { get; init; }

    /// <summary>
    /// The organization (tenant) this event belongs to, or <c>null</c> for a platform-global
    /// event owned by no single tenant. See the type-level remarks for the reconciliation with
    /// the base consumer's "no silent processing without a tenant" rule.
    /// </summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>The event payload, carried opaquely as JSON.</summary>
    public required JsonElement Data { get; init; }

    /// <summary>Shared JSON options — camelCase, used for both envelope and payload (de)serialization.</summary>
    [JsonIgnore]
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>Build an envelope around <paramref name="data"/>, stamping a fresh id and the current UTC time.</summary>
    public static EventEnvelope Create<TData>(string type, TData data, int version = 1, Guid? organizationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type = type,
            Version = version,
            OrganizationId = organizationId,
            Data = JsonSerializer.SerializeToElement(data, JsonOptions),
        };
    }

    /// <summary>Deserialize <see cref="Data"/> into the concrete payload contract <typeparamref name="TData"/>.</summary>
    public TData? DataAs<TData>() => Data.Deserialize<TData>(JsonOptions);
}
