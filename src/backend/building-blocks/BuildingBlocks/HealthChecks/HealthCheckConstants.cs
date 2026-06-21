namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Shared, consistently-named health-check building blocks so every service exposes
/// the same two endpoints and the same readiness tag, letting one Grafana/Prometheus
/// scrape config treat all services uniformly.
/// </summary>
public static class HealthCheckConstants
{
    /// <summary>Liveness endpoint: the process is up. No external dependency is probed.</summary>
    public const string LivenessEndpoint = "/healthz";

    /// <summary>Readiness endpoint: the service can serve traffic (its dependencies are reachable).</summary>
    public const string ReadinessEndpoint = "/readyz";

    /// <summary>Tag applied to readiness checks so the readiness endpoint runs only those.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>Registered name of the Redis readiness check.</summary>
    public const string RedisCheckName = "redis";

    /// <summary>Registered name of the Kafka readiness check.</summary>
    public const string KafkaCheckName = "kafka";

    /// <summary>Registered name of the PostgreSQL readiness check.</summary>
    public const string PostgresCheckName = "postgres";

    /// <summary>Registered name of the MongoDB readiness check.</summary>
    public const string MongoCheckName = "mongo";
}
