using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.BuildingBlocks.Tests;

[TestFixture]
public class EventEnvelopeTests
{
    private sealed record SamplePayload(Guid UserId, string DisplayName);

    [Test]
    public void Create_stamps_id_time_type_and_version()
    {
        var payload = new SamplePayload(Guid.NewGuid(), "Alice");

        var envelope = EventEnvelope.Create(Topics.UserRegistered, payload, version: 2);

        envelope.EventId.Should().NotBeEmpty();
        envelope.Type.Should().Be("user.registered");
        envelope.Version.Should().Be(2);
        envelope.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public void DataAs_round_trips_the_payload()
    {
        var payload = new SamplePayload(Guid.NewGuid(), "Bob");
        var envelope = EventEnvelope.Create(Topics.UserUpdated, payload);

        var read = envelope.DataAs<SamplePayload>();

        read.Should().Be(payload);
    }

    [Test]
    public void Envelope_survives_a_full_json_serialize_deserialize_cycle()
    {
        var payload = new SamplePayload(Guid.NewGuid(), "Carol");
        var original = EventEnvelope.Create(Topics.ExerciseCompleted, payload);

        var json = JsonSerializer.Serialize(original, EventEnvelope.JsonOptions);
        var restored = JsonSerializer.Deserialize<EventEnvelope>(json, EventEnvelope.JsonOptions);

        restored.Should().NotBeNull();
        restored!.EventId.Should().Be(original.EventId);
        restored.Type.Should().Be(original.Type);
        restored.Version.Should().Be(original.Version);
        restored.DataAs<SamplePayload>().Should().Be(payload);
    }

    [Test]
    public void Create_rejects_blank_event_type()
    {
        var act = () => EventEnvelope.Create(" ", new SamplePayload(Guid.NewGuid(), "x"));

        act.Should().Throw<ArgumentException>();
    }
}
