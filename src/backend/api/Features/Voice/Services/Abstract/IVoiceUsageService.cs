using SalesTrainer.Api.Features.Voice.Models;

namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface IVoiceUsageService
{
    Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="VoiceUsageLimitException"/> if the user has already
    /// exceeded the daily or monthly minute cap.
    /// </summary>
    Task EnsureWithinLimitsAsync(Guid userId, CancellationToken ct = default);

    Task RecordSessionSecondsAsync(string sessionId, Guid userId, int seconds, CancellationToken ct = default);
}

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
