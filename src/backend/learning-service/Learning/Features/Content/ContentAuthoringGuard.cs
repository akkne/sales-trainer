using System.Security.Claims;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Content;

/// <summary>
/// Phase 40.18. The one rule that decides who may write a row in a content table, stated once so
/// four admin controllers cannot each invent their own version of it.
///
/// <para>
/// <b>Row-level security does not decide this, and that is the whole point.</b> The content policy
/// is <c>OrganizationId IS NULL OR OrganizationId = current</c> in <em>both</em> the <c>USING</c>
/// and the <c>WITH CHECK</c> clause, because a customer has to be able to read the global library.
/// Read that clause as a write rule and it says: any organization may write a row with a null
/// owner — that is, may edit the curriculum of every other customer. The database cannot tell those
/// two cases apart, because "global" is a null and not a tenant. So the boundary between "my
/// override" and "the shared library" is enforced here, in code, on top of RLS rather than by it.
/// </para>
///
/// <para>
/// The rule: a row with an owning organization belongs to that organization, and RLS has already
/// proved the caller is inside it, so an organization administrator may write it. A row with no
/// owner is the global library and needs platform administrator rights. Creating brand-new content
/// from nothing stays platform-only in this block — an organization customizes what exists, and
/// authoring an original curriculum is a different product question (40.19/40.20).
/// </para>
/// </summary>
internal static class ContentAuthoringGuard
{
    public static bool IsPlatformAdministrator(ClaimsPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return caller.IsInRole(AuthorizationPolicies.AdministratorRole)
               || caller.IsInRole(AuthorizationPolicies.SuperAdministratorRole);
    }

    /// <summary>
    /// True when <paramref name="caller"/> may write a content row owned by
    /// <paramref name="owningOrganizationId"/>. A row that does not exist is <b>allowed</b> here on
    /// purpose: the caller then reports "not found" from the code that looked it up, so an outsider
    /// cannot tell a missing row from somebody else's — the same answer 40.15 gives.
    /// </summary>
    public static bool MayAuthor(ClaimsPrincipal caller, Guid? owningOrganizationId)
        => owningOrganizationId is not null || IsPlatformAdministrator(caller);
}
