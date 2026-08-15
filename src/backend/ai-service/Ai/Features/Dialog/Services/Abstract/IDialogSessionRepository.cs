using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog.Services.Abstract;

/// <summary>
/// Phase 40.11. The single door to the <c>dialog_sessions</c> collection.
///
/// <para>
/// Postgres tables in this codebase are guarded twice: an EF query filter for convenience and a
/// row-level-security policy that is the actual boundary. Mongo has no equivalent of the second,
/// so for dialog sessions the application <em>is</em> the boundary — and a boundary spread over
/// four services is a boundary that will be forgotten on the fifth. Hence this interface: every
/// method is tenant-filtered by construction, none accepts an organization argument, and there is
/// deliberately no "…AllOrganizations" escape hatch. An implementation must refuse to run at all
/// when the tenant is unset rather than silently widening to every organization
/// (roadmap 40.14, "an unset tenant is an exception, never a licence").
/// </para>
///
/// <para>
/// Read docs/TENANCY/TENANCY.md 1.6 before adding a method here: anything new must carry the same
/// filter, and this file is the one place a reviewer has to audit to know that it does.
/// </para>
/// </summary>
public interface IDialogSessionRepository
{
    /// <summary>Inserts a session, stamping it with the current organization.</summary>
    Task InsertAsync(DialogSession session, CancellationToken cancellationToken = default);

    Task<DialogSession?> FindForUserAsync(string sessionId, Guid userId, CancellationToken cancellationToken = default);

    Task<List<DialogSession>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AppendMessagesAsync(
        string sessionId,
        Guid userId,
        IReadOnlyCollection<DialogMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>Marks a session abandoned with zero XP — the "no user messages to evaluate" path.</summary>
    Task AbandonAsync(string sessionId, Guid userId, CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string sessionId,
        Guid userId,
        DialogFeedback feedback,
        int experiencePointsEarned,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteForUserAsync(string sessionId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Adds voice seconds to a session. Returns <c>false</c> when no session matched.</summary>
    Task<bool> IncrementVoiceSecondsAsync(
        string sessionId,
        Guid userId,
        int seconds,
        CancellationToken cancellationToken = default);

    Task<int> SumVoiceSecondsForUserAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-user voice totals for the admin screen — for the current organization only, never the
    /// whole installation. A platform superadmin reaches another organization's numbers the same
    /// way they reach anything else: by impersonating into it (40.9).
    /// </summary>
    Task<List<DialogSessionVoiceUsage>> AggregateVoiceUsageAsync(
        DateTime dayStart,
        DateTime monthStart,
        CancellationToken cancellationToken = default);
}

/// <summary>One user's aggregated voice usage inside the current organization.</summary>
public sealed record DialogSessionVoiceUsage(
    Guid UserId,
    int TotalSeconds,
    int SessionCount,
    DateTime LastCallAt,
    int DailyUsedSeconds,
    int MonthlyUsedSeconds);
