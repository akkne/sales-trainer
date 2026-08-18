namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.33. Asks ai-service whether this organization still has batch allowance, before a sweep
/// claims work it cannot finish.
///
/// <para>
/// The two expensive pipelines — 40.27's content generation and 40.32's batch adaptation — claim a
/// lease with one conditional UPDATE that also spends an attempt, and only then call. Learning about
/// the quota wall after that point costs an attempt and holds a lease for an organization that was
/// never going to be served; three ticks of that and the run is <c>failed</c> for a reason that has
/// nothing to do with the run. So the question is asked first, and asked once per organization per
/// tick rather than per item.
/// </para>
///
/// <para>
/// <b>It never increments anything</b> — the roadmap's warning about double counting. The charge
/// happens exactly once, in ai-service, when a completion comes back. This is a read, so calling it
/// twice changes nothing.
/// </para>
/// </summary>
public interface IAiQuotaClient
{
    /// <summary>
    /// <see langword="true"/> also when the answer could not be obtained: an implementation must fail
    /// open, because refusing on an unreachable preflight would stop every customer's pipeline over a
    /// network blip while their budgets sat untouched.
    /// </summary>
    Task<bool> HasBatchAllowanceAsync(CancellationToken cancellationToken = default);
}
