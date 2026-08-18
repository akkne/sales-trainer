namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// The voice roleplay feature switch and the per-user allowances it is metered against.
///
/// <para>
/// <see cref="MaxRecordingSeconds"/> is enforced twice on purpose: it is the number of seconds
/// reserved from the gate up front, and it is also the deadline the outbound stream is cancelled at.
/// A caller that hangs up early gets the difference refunded, so the reservation is a ceiling rather
/// than a charge. Setting it to zero or below would reserve nothing and cap nothing, which is why the
/// controller falls back to <see cref="DefaultMaxRecordingSeconds"/> instead of honouring it.
/// </para>
///
/// <para>
/// A limit of 0 means the window is disabled, not that it is closed.
/// </para>
/// </summary>
public sealed class VoiceFeatureConfiguration
{
    public const string SectionName = "Voice";

    /// <summary>
    /// Cap used when <see cref="MaxRecordingSeconds"/> is misconfigured to a non-positive value.
    /// Also the shipped default, so a correct configuration and the fallback agree.
    /// </summary>
    public const int DefaultMaxRecordingSeconds = 60;

    public bool Enabled { get; init; } = false;
    public int VadSilenceMilliseconds { get; init; } = 1200;
    public int MaxRecordingSeconds { get; init; } = DefaultMaxRecordingSeconds;
    public int DailyLimitMinutes { get; init; } = 30;
    public int MonthlyLimitMinutes { get; init; } = 300;
}
