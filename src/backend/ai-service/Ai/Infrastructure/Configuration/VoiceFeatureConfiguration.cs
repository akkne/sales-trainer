namespace Sellevate.Ai.Infrastructure.Configuration;

public sealed class VoiceFeatureConfiguration
{
    public const string SectionName = "Voice";

    public bool Enabled { get; init; } = false;
    public int VadSilenceMilliseconds { get; init; } = 600;
    public int MaxRecordingSeconds { get; init; } = 60;
    public int DailyLimitMinutes { get; init; } = 30;
    public int MonthlyLimitMinutes { get; init; } = 300;
}
