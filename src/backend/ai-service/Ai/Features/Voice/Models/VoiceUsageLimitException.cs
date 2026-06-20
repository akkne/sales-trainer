namespace Sellevate.Ai.Features.Voice.Models;

public sealed class VoiceUsageLimitException : Exception
{
    public string Period { get; }
    public int UsedSeconds { get; }
    public int LimitSeconds { get; }

    public VoiceUsageLimitException(string period, int usedSeconds, int limitSeconds)
        : base($"Voice {period} limit reached: {usedSeconds}s / {limitSeconds}s")
    {
        Period = period;
        UsedSeconds = usedSeconds;
        LimitSeconds = limitSeconds;
    }
}
