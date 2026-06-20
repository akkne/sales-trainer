namespace Sellevate.Ai.Features.Voice.Models;

public sealed class VoiceUsageDto
{
    public int DailyUsedSeconds { get; set; }
    public int DailyLimitSeconds { get; set; }
    public int MonthlyUsedSeconds { get; set; }
    public int MonthlyLimitSeconds { get; set; }
    public bool DailyExceeded => DailyLimitSeconds > 0 && DailyUsedSeconds >= DailyLimitSeconds;
    public bool MonthlyExceeded => MonthlyLimitSeconds > 0 && MonthlyUsedSeconds >= MonthlyLimitSeconds;
}
