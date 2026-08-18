namespace Sellevate.Learning.Infrastructure.Identity;

/// <summary>
/// Phase 40.23. Who currently works at the organization in context, asked of the service that owns
/// the answer (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// learning-db holds <c>UserReplicas</c>, which is platform-global by design and says nothing about
/// who belongs where (docs/TENANCY/TENANCY.md §4.2). It therefore cannot resolve an assignment's
/// audience on its own, and this is the seam that lets it.
/// </para>
/// </summary>
public interface IOrganizationMemberDirectory
{
    /// <summary>
    /// The user ids of every active membership in the current organization.
    ///
    /// <para>
    /// Throws when identity-service cannot answer. That is the contract, not an accident: an empty
    /// list and "we could not find out" are the same value in a happy-path world and completely
    /// different facts here — one issues an assignment to nobody and reports success.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(CancellationToken cancellationToken = default);
}
