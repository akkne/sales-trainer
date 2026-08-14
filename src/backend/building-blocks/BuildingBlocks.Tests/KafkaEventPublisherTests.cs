using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.BuildingBlocks.Tests;

/// <summary>
/// Publishing a domain event is a side channel: the change it announces is already committed, so a
/// broker that is down must neither fail nor stall the request that produced it. Regression — a
/// dialog session's feedback was generated in 16s and then sat for 100s inside
/// <c>ProduceAsync</c> (librdkafka's default <c>message.timeout.ms</c> is 5 minutes) while the user
/// watched «Готовим разбор…» and the gateway eventually answered 504.
/// </summary>
[TestFixture]
public class KafkaEventPublisherTests
{
    private const int PublishTimeoutSeconds = 2;

    private static KafkaEventPublisher CreatePublisherPointedAtNothing() =>
        new(
            Options.Create(new KafkaSettings
            {
                // Port 1 has nothing listening — the produce can never be acknowledged.
                BootstrapServers = "127.0.0.1:1",
                PublishTimeoutSeconds = PublishTimeoutSeconds,
            }),
            NullLogger<KafkaEventPublisher>.Instance);

    [Test]
    public async Task Publishing_with_an_unreachable_broker_does_not_delay_the_caller()
    {
        using var publisher = CreatePublisherPointedAtNothing();
        var startedAt = DateTime.UtcNow;

        await publisher.PublishAsync("some.topic", "partition-key", "some.event", new { Value = 1 });

        var elapsed = DateTime.UtcNow - startedAt;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            because: "the event is queued locally — the request that produced it waits for nothing");
    }

    [Test]
    public void Publishing_with_an_unreachable_broker_does_not_throw_at_the_caller()
    {
        using var publisher = CreatePublisherPointedAtNothing();

        var publish = async () =>
            await publisher.PublishAsync("some.topic", "partition-key", "some.event", new { Value = 1 });

        publish.Should().NotThrowAsync();
    }

    [Test]
    public async Task Forwarding_an_outbox_message_still_fails_loudly_so_it_can_be_retried()
    {
        using var publisher = CreatePublisherPointedAtNothing();

        var forward = async () => await publisher.ForwardAsync("some.topic", "partition-key", "{}");

        await forward.Should().ThrowAsync<Exception>(
            because: "a silently 'sent' outbox row would be lost forever");
    }
}
