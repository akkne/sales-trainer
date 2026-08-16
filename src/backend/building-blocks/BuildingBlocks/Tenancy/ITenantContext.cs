namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// The tenant a request belongs to, and the two ways a request can legitimately not belong to
/// exactly one. The three modes are mutually exclusive in their reasons for existing and are
/// deliberately not collapsed into one flag:
///
/// <list type="bullet">
/// <item><description><b>Tenant mode</b> — <see cref="OrganizationId"/> set, both other flags
/// false. The normal case: a member of one organization acting inside it.</description></item>
/// <item><description><b>Platform-wide mode</b> — <see cref="IsPlatformWide"/>. A validated
/// Sellevate staff principal (`role` claim `Admin` or `SuperAdmin`) reading across every
/// organization. There is a human behind the request and a token that proves who they are; only
/// reads widen, writes stay bound to an explicit organization.</description></item>
/// <item><description><b>System mode</b> — <see cref="IsSystem"/>. A background job with no user
/// and no token at all: the outbox relay, a replica consumer, a cleanup service.</description></item>
/// </list>
///
/// <para>
/// Platform-wide and system are not the same thing and must not be merged. System mode exists
/// because there is nobody to attribute the work to; platform-wide mode exists because the person
/// is entitled to see everything. Merging them would let any background job inherit a human's
/// privileges — or worse, let a request quietly acquire a job's.
/// </para>
/// </summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }

    /// <summary>
    /// True when a validated platform-staff principal is reading across organizations. Widens
    /// reads only: EF query filters short-circuit, the RLS <c>USING</c> clause admits every row,
    /// and the Mongo repositories drop the organization from their filter. The RLS
    /// <c>WITH CHECK</c> clause and <c>TenantSaveChangesInterceptor</c> are untouched, so a write
    /// still needs an explicit organization.
    /// </summary>
    bool IsPlatformWide { get; }

    bool IsSystem { get; }
}
