using Sellevate.Ai.Features.Voice.Models;

namespace Sellevate.Ai.Features.Voice.Services.Abstract;

public interface IVoiceUsageService
{
    Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AI1: Atomically reserves up to <paramref name="maxSeconds"/> against the Redis gate counter
    /// for the current day and month windows. Throws <see cref="VoiceUsageLimitException"/> if the
    /// limit would be exceeded. Returns the number of seconds actually reserved.
    /// </summary>
    Task<int> ReserveSecondsAsync(Guid userId, int maxSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// AI1: Adjusts the Redis reservation and records the actual elapsed seconds in Mongo.
    /// Call from the finally block; <paramref name="reservedSeconds"/> - <paramref name="actualSeconds"/>
    /// is refunded back to the Redis counter so other concurrent streams get accurate headroom.
    /// </summary>
    Task RefundReservationAsync(string sessionId, Guid userId, int reservedSeconds, int actualSeconds, CancellationToken cancellationToken = default);

    Task RecordSessionSecondsAsync(string sessionId, Guid userId, int seconds, CancellationToken cancellationToken = default);
    Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken cancellationToken = default);
}
