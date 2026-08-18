using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// Body of <c>POST /admin/platform/impersonation</c>.
///
/// <para>
/// This is the **one** place in the backend where an organization identifier legitimately arrives
/// in a request body. docs/TENANCY/TENANCY.md §1.3 states the rule and its single exception in the
/// same breath: "the organization is never read from the request body, query string, or route […]
/// A superadmin acting across tenants does so through an explicit impersonation endpoint that
/// mints a new token — never through a parameter on an ordinary endpoint." Naming the organization
/// is the entire purpose of this endpoint, and the endpoint is gated by <c>RequireSuperAdmin</c>.
/// </para>
///
/// <para>
/// <c>scripts/tenancy-boundary-lint.py</c> knows about this file by path and about no other, so
/// the rule stays mechanically enforced everywhere else — see the allow-list in that script.
/// </para>
/// </summary>
public sealed record CreateImpersonationRequestDto(
    [Required] Guid OrganizationId,
    [Required, MinLength(3), MaxLength(500)] string Reason);
