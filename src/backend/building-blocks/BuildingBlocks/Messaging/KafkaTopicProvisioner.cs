using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Creates every known topic (and its <c>.dlt</c> companion) on startup so the platform does
/// not depend on the broker's <c>auto.create.topics.enable</c> setting. Without this, a consumer
/// subscribing to a not-yet-existing topic on a hardened/managed broker fails the consume loop
/// with "Subscribed topic not available: … Unknown topic or partition", and no events are ever
/// delivered.
///
/// <para>
/// Registered as the first hosted service (in <c>AddSellevateEventing</c>), so its
/// <see cref="StartAsync"/> completes — topics exist — before the consumer background services
/// subscribe. Creation is idempotent: a topic that already exists is treated as success. A broker
/// that is unreachable at startup is logged and tolerated rather than crashing the host; the
/// consume loop self-heals once the topics appear.
/// </para>
/// </summary>
public sealed class KafkaTopicProvisioner : IHostedService
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaTopicProvisioner> _logger;

    public KafkaTopicProvisioner(IOptions<KafkaSettings> settings, ILogger<KafkaTopicProvisioner> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.ProvisionTopics)
        {
            return;
        }

        // Base topics plus their dead-letter companions — both are produced/consumed at runtime.
        var topicNames = Topics.All
            .SelectMany(topic => new[] { topic, Topics.DeadLetterFor(topic) })
            .Distinct()
            .ToArray();

        var specifications = topicNames
            .Select(name => new TopicSpecification
            {
                Name = name,
                NumPartitions = _settings.TopicPartitions,
                ReplicationFactor = _settings.TopicReplicationFactor,
            })
            .ToList();

        try
        {
            var adminConfig = new AdminClientConfig { BootstrapServers = _settings.BootstrapServers };
            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            await adminClient.CreateTopicsAsync(specifications);
            _logger.LogInformation(
                "Provisioned {Count} Kafka topics ({Partitions} partitions, RF {ReplicationFactor})",
                specifications.Count, _settings.TopicPartitions, _settings.TopicReplicationFactor);
        }
        catch (CreateTopicsException exception)
        {
            // Per-topic results: a topic that already exists is the normal, expected case.
            var realFailures = exception.Results
                .Where(result => result.Error.Code != ErrorCode.NoError
                                 && result.Error.Code != ErrorCode.TopicAlreadyExists)
                .ToList();

            if (realFailures.Count == 0)
            {
                _logger.LogInformation(
                    "Kafka topics already present; nothing to provision ({Count} checked)",
                    specifications.Count);
                return;
            }

            foreach (var failure in realFailures)
            {
                _logger.LogError(
                    "Failed to provision Kafka topic {Topic}: {Reason}",
                    failure.Topic, failure.Error.Reason);
            }
        }
        catch (Exception exception)
        {
            // Broker unreachable at startup, etc. Don't crash the host — the consume loop retries
            // and librdkafka refreshes metadata once the broker/topics become available.
            _logger.LogError(exception, "Kafka topic provisioning failed; continuing startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
