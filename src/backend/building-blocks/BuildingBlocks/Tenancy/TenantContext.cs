namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// The scoped, write-once implementation of <see cref="ITenantContext"/>. Registered per request (or
/// per background unit of work) and populated exactly once by <see cref="TenantContextMiddleware"/>,
/// by the consumer base class from the event envelope, or by a background job declaring system mode.
///
/// <para>
/// Every mutator is <b>idempotent but not re-assignable</b>: re-stating the mode a scope is already
/// in is a no-op, so a message retry is safe, while contradicting it throws. That is what makes
/// "which tenant is this scope?" a question with one answer for the scope's whole lifetime — the
/// invariant the EF query filter and the RLS session variable both close over.
/// </para>
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private const string AlreadyInSystemModeMessage = "Tenant context is already in system mode for this scope.";
    private const string OrganizationAlreadySetMessage = "Tenant context organization is already set for this scope.";
    private const string AlreadyPlatformWideMessage = "Tenant context is already in platform-wide mode for this scope.";
    private const string EmptyOrganizationMessage = "Organization id must not be empty.";

    public Guid? OrganizationId { get; private set; }

    public bool IsPlatformWide { get; private set; }

    public bool IsSystem { get; private set; }

    /// <summary>
    /// Binds this scope to <paramref name="organizationId"/>. Setting the same organization again is
    /// a no-op — which is what lets a consumer re-apply the envelope's tenant on a handler retry —
    /// but a different organization, an empty <see cref="Guid"/>, or a scope already in system mode
    /// throws rather than silently rebinding.
    /// </summary>
    public void SetOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(EmptyOrganizationMessage, nameof(organizationId));
        }

        if (IsSystem)
        {
            throw new InvalidOperationException(AlreadyInSystemModeMessage);
        }

        if (OrganizationId is { } alreadySetOrganizationId && alreadySetOrganizationId != organizationId)
        {
            throw new InvalidOperationException(OrganizationAlreadySetMessage);
        }

        OrganizationId = organizationId;
    }

    /// <summary>
    /// Widens reads to every organization for a validated platform-staff principal. Deliberately
    /// compatible with <see cref="SetOrganization"/>: a Sellevate administrator who also belongs to
    /// an organization keeps that organization, because it is the one their writes land in — the
    /// widening applies to what they can see, not to where their changes go.
    ///
    /// <para>
    /// Not compatible with <see cref="EnterSystemMode"/> in either direction. A background job has
    /// no principal to be entitled by, so a scope that is both is a bug, not a broader privilege.
    /// </para>
    /// </summary>
    public void EnterPlatformMode()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException(AlreadyInSystemModeMessage);
        }

        IsPlatformWide = true;
    }

    /// <summary>
    /// Declares that this scope belongs to a background job with no principal at all — the outbox
    /// relay, a replica consumer, a cleanup service. Re-entering is a no-op, so a retried unit of
    /// work is safe.
    ///
    /// <para>
    /// Mutually exclusive with both other modes in both directions. A scope that carries an
    /// organization, or that is platform-wide, has an attributable caller behind it, and letting a
    /// job inherit that — or a request inherit a job's unscoped reach — is the merge
    /// <see cref="ITenantContext"/> exists to prevent.
    /// </para>
    /// </summary>
    public void EnterSystemMode()
    {
        if (OrganizationId is not null)
        {
            throw new InvalidOperationException(OrganizationAlreadySetMessage);
        }

        if (IsPlatformWide)
        {
            throw new InvalidOperationException(AlreadyPlatformWideMessage);
        }

        IsSystem = true;
    }
}
