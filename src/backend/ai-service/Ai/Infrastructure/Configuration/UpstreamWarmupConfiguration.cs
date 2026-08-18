namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Timings of the connection warmup that keeps TCP+TLS to the paid providers open.
///
/// <para>
/// <see cref="IntervalMinutes"/> must stay below the HTTP handler's pooled-connection idle timeout, or
/// the sockets it exists to keep alive are already closed when it fires and the warmup does nothing but
/// cost two requests. <see cref="PerTargetTimeoutSeconds"/> bounds one probe: a provider that is slow to
/// answer a HEAD is a provider whose socket is not worth waiting on, since nothing depends on the warmup
/// succeeding.
/// </para>
/// </summary>
public sealed class UpstreamWarmupConfiguration
{
    public const string SectionName = "UpstreamWarmup";

    /// <summary>How long one provider's probe may take before it is abandoned.</summary>
    public int PerTargetTimeoutSeconds { get; init; } = 5;

    /// <summary>How often every configured provider is re-probed.</summary>
    public int IntervalMinutes { get; init; } = 4;
}
