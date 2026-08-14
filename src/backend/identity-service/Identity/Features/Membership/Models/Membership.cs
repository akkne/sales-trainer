namespace Sellevate.Identity.Features.Membership.Models;

/// <summary>
/// A user's membership in one organization: which role they hold there, whether it is
/// still active, and who invited them. Primary key <c>(UserId, OrganizationId)</c> exists
/// from day one (40.6) even though the current UI only ever creates one row per user —
/// retrofitting a join table later would mean rewriting the JWT, every authorization
/// check and the invite flow at once. See docs/TENANCY/TENANCY.md §4.2.
///
/// <para>
/// <see cref="OrganizationId"/> is a bare <c>uuid</c> with no foreign key: this service
/// has its own database (DB-per-service) and <c>organization-service</c> owns the
/// registry. A user with no <see cref="Membership"/> row has no organization access —
/// absence is never implicit privilege.
/// </para>
/// </summary>
public sealed class Membership
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public OrgRole Role { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public Guid? InvitedBy { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
