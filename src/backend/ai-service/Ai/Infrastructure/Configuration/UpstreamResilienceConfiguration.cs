namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Timeouts, retries and circuit-breaker shape of every outbound call to a paid provider (AI6).
///
/// <para>
/// The four timeouts are not independent. <see cref="HandlerTimeoutSeconds"/> is deliberately the
/// largest — it is <see cref="HttpClient.Timeout"/>, an outer guard that must never fire before the
/// resilience pipeline has finished, or a retry is cut off mid-flight and the failure is reported as a
/// client timeout instead of the provider fault it was. Inside it,
/// <see cref="AttemptTimeoutSeconds"/> bounds one attempt and
/// <see cref="TotalRequestTimeoutSeconds"/> bounds the whole ladder.
/// </para>
///
/// <para>
/// <b>Polly validates the breaker at host startup:</b> <see cref="CircuitBreakerSamplingSeconds"/>
/// must be at least twice <see cref="AttemptTimeoutSeconds"/>. Lowering the sampling window without
/// lowering the attempt timeout does not misbehave at runtime — it refuses to start.
/// </para>
/// </summary>
public sealed class UpstreamResilienceConfiguration
{
    public const string SectionName = "UpstreamResilience";

    /// <summary>Outer <see cref="HttpClient.Timeout"/>. Must exceed <see cref="TotalRequestTimeoutSeconds"/>.</summary>
    public int HandlerTimeoutSeconds { get; init; } = 90;

    /// <summary>How long one attempt may take.</summary>
    public int AttemptTimeoutSeconds { get; init; } = 30;

    /// <summary>Retries after the first attempt, on 5xx, 429 or timeout.</summary>
    public int MaximumRetryAttempts { get; init; } = 2;

    /// <summary>Base delay between retries.</summary>
    public int RetryDelaySeconds { get; init; } = 1;

    /// <summary>Breaker window. At least twice <see cref="AttemptTimeoutSeconds"/> or the host will not start.</summary>
    public int CircuitBreakerSamplingSeconds { get; init; } = 60;

    /// <summary>Calls that must land in the window before the breaker will consider opening.</summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 5;

    /// <summary>Ceiling across every attempt and delay for one logical call.</summary>
    public int TotalRequestTimeoutSeconds { get; init; } = 90;

    /// <summary>How long an idle pooled socket is kept, so the warmup has something to keep warm.</summary>
    public int PooledConnectionIdleTimeoutMinutes { get; init; } = 10;

    /// <summary>Hard lifetime of a pooled socket, so DNS changes are eventually picked up.</summary>
    public int PooledConnectionLifetimeMinutes { get; init; } = 30;

    /// <summary>How long <see cref="IHttpClientFactory"/> reuses one handler chain.</summary>
    public int HandlerLifetimeMinutes { get; init; } = 30;
}
