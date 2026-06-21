using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Readiness check that confirms the Kafka broker is reachable by requesting cluster
/// metadata through a short-lived admin client. Reports <see cref="HealthStatus.Unhealthy"/>
/// when the broker cannot be contacted within the probe timeout.
/// </summary>
public sealed class KafkaHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly KafkaSettings _settings;

    public KafkaHealthCheck(IOptions<KafkaSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var adminConfig = new AdminClientConfig { BootstrapServers = _settings.BootstrapServers };
            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            adminClient.GetMetadata(ProbeTimeout);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka is not reachable.", exception));
        }
    }
}
