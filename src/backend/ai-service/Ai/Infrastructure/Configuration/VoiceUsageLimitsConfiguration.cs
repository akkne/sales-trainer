namespace Sellevate.Ai.Infrastructure.Configuration;

public sealed class VoiceUsageLimitsConfiguration
{
    public const string SectionName = "Voice";

    public int DailyLimitMinutes { get; init; } = 0;
    public int MonthlyLimitMinutes { get; init; } = 0;
}
