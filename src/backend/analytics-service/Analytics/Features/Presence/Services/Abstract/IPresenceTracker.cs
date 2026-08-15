namespace Sellevate.Analytics.Features.Presence.Services.Abstract;

/// <summary>
/// Phase 40.13. Presence is the one piece of state analytics-service actually stores, and it is
/// per-organization state: "who is online" is a fact about one customer's team. Every member of
/// this interface therefore names its scope in its signature — there is no method that means
/// "current organization, whichever that is", because a singleton has no current organization and
/// an ambient one read at the wrong moment is how the count of customer A's staff ends up on
/// customer B's screen.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>
    /// Records that <paramref name="userId"/> of <paramref name="organizationId"/> was just seen.
    /// The organization is a parameter, not ambient state: the caller is a request handler that
    /// already knows it, and passing it through keeps the Redis key derivable from the signature.
    /// </summary>
    Task MarkSeenAsync(Guid organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many of <paramref name="organizationId"/>'s users are inside the presence window. This
    /// is the only per-tenant read, and it can never widen: the key it looks at contains exactly
    /// one organization's members.
    /// </summary>
    Task<long> CountOnlineAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The platform-wide total, summed over every organization that has recorded presence. Named
    /// for what it does rather than defaulting to it: this is the deliberate system-mode read
    /// (docs/TENANCY/TENANCY.md §1.6) that feeds the operational <c>app_users_online</c> gauge, and
    /// it must stay unreachable from any customer-facing route.
    /// </summary>
    Task<long> CountOnlineAcrossAllOrganizationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Compaction only — drops members that fell out of the presence window, and forgets an
    /// organization once its set is empty so the registry does not grow forever.
    /// </summary>
    Task PruneAsync(CancellationToken cancellationToken = default);
}
