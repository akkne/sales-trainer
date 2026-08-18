using Sellevate.Ai.Features.Voice.Models;

namespace Sellevate.Ai.Features.Voice.Services.Abstract;

/// <summary>
/// The voice reservation gate. Callers must treat reserve and refund as a pair: reserve before the
/// first provider call, refund from a <c>finally</c> block, or a learner's allowance is consumed by
/// seconds nobody spoke.
/// </summary>
public interface IVoiceUsageService
{
    /// <summary>The caller's own used and allowed seconds, for both windows. Reads Mongo, not the gate.</summary>
    Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves up to <paramref name="maxSeconds"/> against the day window, the month
    /// window and the organization quota, in that order, returning the seconds actually reserved.
    ///
    /// <para>
    /// Throws <see cref="VoiceUsageLimitException"/> when any window would be exceeded, having first
    /// rolled back whatever the earlier windows already took — so a refusal never leaves seconds held.
    /// </para>
    /// </summary>
    Task<int> ReserveSecondsAsync(Guid userId, int maxSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a reservation: returns <c>reservedSeconds - actualSeconds</c> to the gate so concurrent
    /// streams see accurate headroom, and records the billable remainder durably in Mongo.
    ///
    /// <para>
    /// Call it from a <c>finally</c> block with a non-cancellable token — it must run on a client
    /// disconnect, which is exactly when the reservation is largest and the actual usage smallest.
    /// <paramref name="actualSeconds"/> above <paramref name="reservedSeconds"/> is clamped, never
    /// charged.
    /// </para>
    /// </summary>
    Task RefundReservationAsync(string sessionId, Guid userId, int reservedSeconds, int actualSeconds, CancellationToken cancellationToken = default);

    /// <summary>Adds seconds to the session's durable total. A non-positive value is a no-op.</summary>
    Task RecordSessionSecondsAsync(string sessionId, Guid userId, int seconds, CancellationToken cancellationToken = default);

    /// <summary>Per-user spend for the caller's organization, plus that organization's own limits.</summary>
    Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken cancellationToken = default);
}
